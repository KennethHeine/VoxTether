using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using VoxTether.Core.Interfaces;
using VoxTether.Core.Models;
using VoxTether.Transcription;

namespace VoxTether.Diagnostics;

/// <summary>
/// Command-line diagnostic tool for VoxTether CUDA/transcription issues.
/// </summary>
public static class Program
{
    // ANSI color codes
    private const string Red = "\x1b[31m";
    private const string Green = "\x1b[32m";
    private const string Yellow = "\x1b[33m";
    private const string Cyan = "\x1b[36m";
    private const string Reset = "\x1b[0m";
    private const string Bold = "\x1b[1m";

    private static string? _modelsPath;
    private static string? _whisperPath;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        
        // Enable ANSI colors on Windows
        EnableAnsiColors();

        PrintHeader();

        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            PrintHelp();
            return 0;
        }

        // Initialize paths
        _modelsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoxTether", "models");
        _whisperPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoxTether", "whisper");

        var command = args[0].ToLowerInvariant();
        
        try
        {
            return command switch
            {
                "info" or "system" => await RunSystemInfo(),
                "check" or "validate" => await RunValidation(),
                "cuda" => await RunCudaTest(args.Skip(1).ToArray()),
                "cpu" => await RunCpuTest(args.Skip(1).ToArray()),
                "test" => await RunTranscriptionTest(args.Skip(1).ToArray()),
                "paths" => ShowPaths(),
                "dlls" => CheckCudaDlls(),
                "dll-versions" => CheckDllVersions(),
                "run" => await RunWhisperDirect(args.Skip(1).ToArray()),
                "copy-dlls" => CopyCudaDlls(),
                "download-dlls" => await DownloadNvidiaCudaDlls(),
                "build-info" => ShowBuildInstructions(),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            PrintError($"Error: {ex.Message}");
            if (args.Contains("--verbose") || args.Contains("-v"))
            {
                Console.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static void PrintHeader()
    {
        Console.WriteLine($@"
{Cyan}╔══════════════════════════════════════════════════════════════╗
║           VoxTether Diagnostics Tool v1.0                    ║
║           CUDA & Transcription Troubleshooter                ║
╚══════════════════════════════════════════════════════════════╝{Reset}
");
    }

    private static void PrintHelp()
    {
        Console.WriteLine($@"
{Bold}USAGE:{Reset}
    voxtether-diag <command> [options]

{Bold}COMMANDS:{Reset}
    {Cyan}info{Reset}, {Cyan}system{Reset}     Show system information (GPU, CUDA, paths)
    {Cyan}check{Reset}, {Cyan}validate{Reset}  Validate backend availability and configuration
    {Cyan}paths{Reset}            Show all VoxTether paths
    {Cyan}dlls{Reset}             Check for CUDA DLLs in various locations
    
    {Cyan}cuda{Reset} [model]      Test CUDA backend with optional model name
    {Cyan}cpu{Reset} [model]       Test CPU backend with optional model name
    {Cyan}test{Reset} [model]      Test transcription with a sample recording
    
    {Cyan}dll-versions{Reset}     Compare CUDA DLL versions (system vs bundled)
    {Cyan}copy-dlls{Reset}        Copy CUDA DLLs from system to whisper folder
    {Cyan}download-dlls{Reset}    Download compatible CUDA DLLs from NVIDIA (~400MB)
    
    {Cyan}build-info{Reset}       Show instructions for building whisper.cpp from source
    
    {Cyan}run{Reset} <args...>     Run whisper-cli.exe directly with custom arguments

{Bold}EXAMPLES:{Reset}
    voxtether-diag info              # Show system info
    voxtether-diag check             # Validate all backends
    voxtether-diag cuda              # Test CUDA with default model
    voxtether-diag cuda small.en     # Test CUDA with small.en model
    voxtether-diag cpu               # Test CPU backend
    voxtether-diag build-info        # How to build whisper.cpp
    voxtether-diag run --help        # Show whisper-cli help

{Bold}OPTIONS:{Reset}
    --verbose, -v    Show detailed output
    --help, -h       Show this help message
");
    }

    private static int UnknownCommand(string command)
    {
        PrintError($"Unknown command: {command}");
        Console.WriteLine("Run 'voxtether-diag --help' for usage information.");
        return 1;
    }

    private static async Task<int> RunSystemInfo()
    {
        PrintSection("System Information");

        // OS Info
        Console.WriteLine($"  OS:              {RuntimeInformation.OSDescription}");
        Console.WriteLine($"  Architecture:    {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"  .NET Runtime:    {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine();

        // GPU Detection using BackendSelectionService
        PrintSection("GPU Detection");
        
        using var loggerFactory = LoggerFactory.Create(b => { });
        var logger = loggerFactory.CreateLogger<BackendSelectionService>();
        var backendService = new BackendSelectionService(logger, skipRuntimeValidation: true);
        
        var gpuDiag = backendService.GetGpuDiagnostics();
        Console.WriteLine($"  NVIDIA GPU:      {BoolStatus(gpuDiag.HasNvidiaGpu)}");
        Console.WriteLine($"  Intel GPU:       {BoolStatus(gpuDiag.HasIntelGpu)}");
        Console.WriteLine($"  AMD GPU:         {BoolStatus(gpuDiag.HasAmdGpu)}");
        Console.WriteLine();

        if (gpuDiag.DetectedGpus.Any())
        {
            Console.WriteLine("  Detected GPUs:");
            foreach (var gpu in gpuDiag.DetectedGpus)
            {
                Console.WriteLine($"    • {gpu}");
            }
            Console.WriteLine();
        }

        // Check nvidia-smi
        PrintSection("NVIDIA Driver Check");
        await CheckNvidiaSmi();

        // CUDA Toolkit
        PrintSection("CUDA Toolkit");
        CheckCudaToolkit();

        // Check for CUDA DLLs
        PrintSection("CUDA Runtime DLLs");
        CheckCudaDlls();

        return 0;
    }

    private static async Task CheckNvidiaSmi()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,driver_version,memory.total,compute_cap --format=csv,noheader",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var parts = output.Trim().Split(',');
                    if (parts.Length >= 4)
                    {
                        Console.WriteLine($"  GPU Name:        {parts[0].Trim()}");
                        Console.WriteLine($"  Driver Version:  {parts[1].Trim()}");
                        Console.WriteLine($"  GPU Memory:      {parts[2].Trim()}");
                        Console.WriteLine($"  Compute Cap:     {parts[3].Trim()}");
                    }
                    else
                    {
                        Console.WriteLine($"  {output.Trim()}");
                    }
                    PrintOk("nvidia-smi found and working");
                }
                else
                {
                    PrintWarning("nvidia-smi returned no output");
                }
            }
        }
        catch (Exception ex)
        {
            PrintWarning($"nvidia-smi not available: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static void CheckCudaToolkit()
    {
        var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
        if (!string.IsNullOrEmpty(cudaPath) && Directory.Exists(cudaPath))
        {
            PrintOk($"CUDA_PATH: {cudaPath}");
            
            var nvccPath = Path.Combine(cudaPath, "bin", "nvcc.exe");
            if (File.Exists(nvccPath))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = nvccPath,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(psi);
                    var output = process?.StandardOutput.ReadToEnd() ?? "";
                    process?.WaitForExit();
                    
                    // Parse version from output like "Cuda compilation tools, release 11.8, V11.8.89"
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"release (\d+\.\d+)");
                    if (match.Success)
                    {
                        Console.WriteLine($"  CUDA Version:    {match.Groups[1].Value}");
                    }
                }
                catch { /* Ignore errors */ }
            }
        }
        else
        {
            PrintWarning("CUDA_PATH not set or not found");
        }
        Console.WriteLine();
    }

    private static int CheckCudaDlls()
    {
        var requiredDlls = BackendSelectionService.RequiredCudaDlls;
        
        Console.WriteLine("  Required DLLs:");
        foreach (var dll in requiredDlls)
        {
            Console.WriteLine($"    • {dll}");
        }
        Console.WriteLine();

        // Check in whisper cuda folder
        var whisperCudaDir = Path.Combine(_whisperPath ?? "", "cuda", "Release");
        Console.WriteLine($"  Whisper CUDA dir: {whisperCudaDir}");
        if (Directory.Exists(whisperCudaDir))
        {
            var allFound = true;
            foreach (var dll in requiredDlls)
            {
                var path = Path.Combine(whisperCudaDir, dll);
                if (File.Exists(path))
                {
                    PrintOk($"    Found: {dll}");
                }
                else
                {
                    PrintError($"    Missing: {dll}");
                    allFound = false;
                }
            }
            if (allFound)
            {
                PrintOk("  All CUDA DLLs present in whisper folder");
            }
        }
        else
        {
            PrintWarning($"  Directory not found: {whisperCudaDir}");
        }
        Console.WriteLine();

        // Check system PATH
        Console.WriteLine("  Checking system PATH for CUDA DLLs...");
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var pathDirs = pathEnv.Split(Path.PathSeparator);
        
        var foundInPath = new Dictionary<string, string>();
        foreach (var dll in requiredDlls)
        {
            foreach (var dir in pathDirs)
            {
                try
                {
                    var dllPath = Path.Combine(dir, dll);
                    if (File.Exists(dllPath))
                    {
                        foundInPath[dll] = dllPath;
                        break;
                    }
                }
                catch { /* Ignore invalid paths */ }
            }
        }

        foreach (var dll in requiredDlls)
        {
            if (foundInPath.TryGetValue(dll, out var path))
            {
                PrintOk($"    {dll}: {path}");
            }
            else
            {
                PrintError($"    {dll}: NOT FOUND in PATH");
            }
        }
        Console.WriteLine();

        return 0;
    }

    private static int CheckDllVersions()
    {
        PrintSection("CUDA DLL Version Comparison");

        var requiredDlls = BackendSelectionService.RequiredCudaDlls;
        var whisperCudaDir = Path.Combine(_whisperPath ?? "", "cuda", "Release");
        
        // Find system CUDA DLLs
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var pathDirs = pathEnv.Split(Path.PathSeparator);
        
        Console.WriteLine("  Comparing DLL versions (System vs Whisper folder):");
        Console.WriteLine();
        Console.WriteLine("  ┌───────────────────────┬────────────────────────────┬────────────────────────────┐");
        Console.WriteLine("  │  DLL Name             │  System (CUDA Toolkit)     │  Whisper Folder            │");
        Console.WriteLine("  ├───────────────────────┼────────────────────────────┼────────────────────────────┤");

        foreach (var dll in requiredDlls)
        {
            // Find in system PATH
            string? systemPath = null;
            foreach (var dir in pathDirs)
            {
                try
                {
                    var dllPath = Path.Combine(dir, dll);
                    if (File.Exists(dllPath))
                    {
                        systemPath = dllPath;
                        break;
                    }
                }
                catch { }
            }

            // Find in whisper folder
            var whisperPath = Path.Combine(whisperCudaDir, dll);
            var whisperExists = File.Exists(whisperPath);

            var systemVersion = systemPath != null ? GetDllVersion(systemPath) : "(not found)";
            var whisperVersion = whisperExists ? GetDllVersion(whisperPath) : "(not found)";

            Console.WriteLine($"  │ {dll,-21} │ {systemVersion,-26} │ {whisperVersion,-26} │");
        }

        Console.WriteLine("  └───────────────────────┴────────────────────────────┴────────────────────────────┘");
        Console.WriteLine();

        // Provide recommendation
        Console.WriteLine("  Recommendation:");
        if (Directory.Exists(whisperCudaDir))
        {
            var hasBundledDlls = requiredDlls.All(dll => File.Exists(Path.Combine(whisperCudaDir, dll)));
            if (hasBundledDlls)
            {
                PrintOk("  CUDA DLLs are bundled with whisper. The bundled DLLs should be compatible.");
                Console.WriteLine("    If CUDA still crashes, the bundled DLLs may be from a different CUDA patch version.");
            }
            else
            {
                PrintWarning("  CUDA DLLs are missing from the whisper folder.");
                Console.WriteLine("    Run 'voxtether-diag copy-dlls' to copy them from your CUDA toolkit.");
                Console.WriteLine("    Note: This may cause version mismatch issues.");
            }
        }

        Console.WriteLine();
        return 0;
    }

    private static string GetDllVersion(string path)
    {
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(path);
            if (!string.IsNullOrEmpty(versionInfo.FileVersion))
            {
                return versionInfo.FileVersion;
            }
            // Fall back to file size as a rough indicator
            var fileInfo = new FileInfo(path);
            return $"(size: {fileInfo.Length / 1024} KB)";
        }
        catch
        {
            return "(error reading)";
        }
    }

    private static int CopyCudaDlls()
    {
        PrintSection("Copy CUDA DLLs from System");

        var requiredDlls = BackendSelectionService.RequiredCudaDlls;
        var whisperCudaDir = Path.Combine(_whisperPath ?? "", "cuda", "Release");

        if (!Directory.Exists(whisperCudaDir))
        {
            PrintError($"Whisper CUDA folder not found: {whisperCudaDir}");
            return 1;
        }

        // Find system CUDA path
        var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
        var cudaBinPath = cudaPath != null ? Path.Combine(cudaPath, "bin") : null;

        if (cudaBinPath == null || !Directory.Exists(cudaBinPath))
        {
            PrintError("CUDA_PATH environment variable not set or bin folder not found");
            return 1;
        }

        Console.WriteLine($"  Source: {cudaBinPath}");
        Console.WriteLine($"  Destination: {whisperCudaDir}");
        Console.WriteLine();

        var copied = 0;
        var failed = 0;

        foreach (var dll in requiredDlls)
        {
            var sourcePath = Path.Combine(cudaBinPath, dll);
            var destPath = Path.Combine(whisperCudaDir, dll);

            if (!File.Exists(sourcePath))
            {
                PrintWarning($"  {dll}: Not found in CUDA bin folder");
                continue;
            }

            try
            {
                File.Copy(sourcePath, destPath, overwrite: true);
                PrintOk($"  Copied: {dll}");
                copied++;
            }
            catch (Exception ex)
            {
                PrintError($"  Failed to copy {dll}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  Result: {copied} copied, {failed} failed");
        
        if (copied > 0)
        {
            PrintWarning(@"
  NOTE: Copying DLLs from your system CUDA toolkit may cause version mismatch.
  The whisper.cpp binary was compiled against a specific cuBLAS version.
  If transcription still crashes, consider:
  1. Using CPU mode (works reliably)
  2. Building whisper.cpp from source with your CUDA toolkit");
        }

        return failed > 0 ? 1 : 0;
    }

    private static async Task<int> DownloadNvidiaCudaDlls()
    {
        PrintSection("Download CUDA DLLs from NVIDIA");

        var whisperCudaDir = Path.Combine(_whisperPath ?? "", "cuda", "Release");

        if (!Directory.Exists(whisperCudaDir))
        {
            PrintError($"Whisper CUDA folder not found: {whisperCudaDir}");
            Console.WriteLine("  Please download the CUDA backend first from VoxTether Settings.");
            return 1;
        }

        Console.WriteLine($"  Destination: {whisperCudaDir}");
        Console.WriteLine();
        Console.WriteLine("  This will download cuBLAS 11.11.3.6 from NVIDIA (~400MB)");
        Console.WriteLine("  which is compatible with the whisper.cpp v1.8.3 CUDA binary.");
        Console.WriteLine();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        var selectionLogger = loggerFactory.CreateLogger<BackendSelectionService>();
        var downloadLogger = loggerFactory.CreateLogger<BackendDownloadService>();
        
        var backendService = new BackendSelectionService(selectionLogger, skipRuntimeValidation: true);
        var downloadService = new BackendDownloadService(downloadLogger, backendService);

        var progress = new Progress<BackendDownloadProgress>(p =>
        {
            if (p.Status == BackendDownloadStatus.Downloading)
            {
                var percent = p.TotalBytes > 0 ? (double)p.BytesDownloaded / p.TotalBytes * 100 : 0;
                Console.Write($"\r  Downloading: {p.Message} ({percent:F1}%)     ");
            }
            else if (p.Status == BackendDownloadStatus.Extracting)
            {
                Console.WriteLine();
                Console.WriteLine($"  {p.Message}");
            }
            else if (p.Status == BackendDownloadStatus.Failed)
            {
                Console.WriteLine();
                PrintError($"  {p.Message}: {p.ErrorMessage}");
            }
            else if (p.Status == BackendDownloadStatus.Completed)
            {
                Console.WriteLine();
                PrintOk($"  {p.Message}");
            }
        });

        Console.WriteLine("  Starting download...");
        var result = await downloadService.DownloadCudaDllsAsync(progress);
        Console.WriteLine();

        if (result)
        {
            PrintOk("  CUDA DLLs downloaded and extracted successfully!");
            Console.WriteLine();
            Console.WriteLine("  Now run 'voxtether-diag cuda' to test CUDA transcription.");
        }
        else
        {
            PrintError("  Failed to download CUDA DLLs");
            Console.WriteLine("  Check the log output above for details.");
        }

        return result ? 0 : 1;
    }

    private static async Task<int> RunValidation()
    {
        PrintSection("Backend Validation");

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<BackendSelectionService>();
        var backendService = new BackendSelectionService(logger, skipRuntimeValidation: false);

        var backends = backendService.GetAvailableBackends();

        Console.WriteLine();
        Console.WriteLine("  ┌────────────┬────────────┬─────────────────────────────────────────┐");
        Console.WriteLine("  │  Backend   │   Status   │  Path / Reason                          │");
        Console.WriteLine("  ├────────────┼────────────┼─────────────────────────────────────────┤");

        foreach (var backend in backends)
        {
            var status = backend.IsAvailable 
                ? $"{Green}Available{Reset}" 
                : $"{Red}Unavailable{Reset}";
            
            var pathOrReason = backend.IsAvailable 
                ? TruncatePath(backend.ExecutablePath ?? "", 38)
                : TruncatePath(backend.UnavailableReason ?? "Unknown", 38);

            Console.WriteLine($"  │ {backend.Backend,-10} │ {status,-19} │ {pathOrReason,-39} │");
        }
        Console.WriteLine("  └────────────┴────────────┴─────────────────────────────────────────┘");
        Console.WriteLine();

        // Run validation test for each available backend
        foreach (var backend in backends.Where(b => b.IsAvailable))
        {
            Console.WriteLine($"  Testing {backend.Backend} backend...");
            await TestWhisperExecutable(backend.ExecutablePath!, backend.Backend.ToString());
            Console.WriteLine();
        }

        return 0;
    }

    private static async Task TestWhisperExecutable(string execPath, string backendName)
    {
        var execDir = Path.GetDirectoryName(execPath) ?? "";

        // First just run --help to check if the executable works
        var psi = new ProcessStartInfo
        {
            FileName = execPath,
            Arguments = "--help",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = execDir
        };

        try
        {
            var sw = Stopwatch.StartNew();
            using var process = Process.Start(psi);
            if (process == null)
            {
                PrintError($"    Failed to start {backendName} executable");
                return;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completed = await Task.Run(() => process.WaitForExit(10000));
            sw.Stop();

            if (!completed)
            {
                process.Kill();
                PrintError($"    {backendName} executable timed out after 10s");
                return;
            }

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode == 0)
            {
                PrintOk($"    {backendName} executable runs OK ({sw.ElapsedMilliseconds}ms)");
                
                // Check for GPU info in output
                if (output.Contains("CUDA devices") || error.Contains("CUDA devices"))
                {
                    var cudaInfo = ExtractCudaInfo(output + error);
                    if (!string.IsNullOrEmpty(cudaInfo))
                    {
                        Console.WriteLine($"      {Cyan}{cudaInfo}{Reset}");
                    }
                }
            }
            else
            {
                PrintError($"    {backendName} executable failed with exit code {process.ExitCode} (0x{process.ExitCode:X8})");
                
                // Explain common error codes
                var explanation = process.ExitCode switch
                {
                    -1073740791 => "STATUS_STACK_BUFFER_OVERRUN - CUDA version mismatch",
                    -1073741515 => "STATUS_DLL_NOT_FOUND - Missing DLL dependency",
                    -1073741819 => "STATUS_ACCESS_VIOLATION - Memory access error",
                    _ => null
                };
                if (explanation != null)
                {
                    PrintWarning($"      {explanation}");
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine($"      stderr: {error.Trim().Split('\n').FirstOrDefault()}");
                }
            }
        }
        catch (Exception ex)
        {
            PrintError($"    Failed to test {backendName}: {ex.Message}");
        }
    }

    private static string ExtractCudaInfo(string output)
    {
        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("Device 0:"))
            {
                return line.Trim();
            }
        }
        return "";
    }

    private static async Task<int> RunCudaTest(string[] args)
    {
        PrintSection("CUDA Backend Test");

        var modelName = args.FirstOrDefault() ?? "small.en";
        var modelPath = FindModel(modelName);

        if (modelPath == null)
        {
            PrintError($"Model not found: {modelName}");
            Console.WriteLine($"  Searched in: {_modelsPath}");
            ListAvailableModels();
            return 1;
        }

        var cudaExePath = FindCudaExecutable();
        if (cudaExePath == null)
        {
            PrintError("CUDA whisper executable not found");
            return 1;
        }

        Console.WriteLine($"  Executable: {cudaExePath}");
        Console.WriteLine($"  Model:      {modelPath}");
        Console.WriteLine();

        // First test --help
        Console.WriteLine("  Step 1: Testing basic execution (--help)...");
        var helpResult = await RunWhisperCommand(cudaExePath, "--help");
        if (helpResult != 0)
        {
            PrintError($"  whisper-cli --help failed with exit code {helpResult}");
            return helpResult;
        }
        PrintOk("  Basic execution works");
        Console.WriteLine();

        // Test with --no-gpu flag (disable CUDA, use CPU path in same binary)
        Console.WriteLine("  Step 2: Testing with --no-gpu flag...");
        var testWavPath = Path.Combine(Path.GetTempPath(), "voxtether-cuda-test.wav");
        CreateSilentWav(testWavPath, durationSeconds: 1);
        
        var noGpuResult = await RunWhisperCommand(cudaExePath, 
            $"-m \"{modelPath}\" -f \"{testWavPath}\" --no-timestamps --no-gpu", 
            verbose: true);
        
        if (noGpuResult == 0)
        {
            PrintOk("  Transcription with --no-gpu works");
        }
        else
        {
            PrintWarning($"  --no-gpu transcription failed with exit code {noGpuResult}");
        }
        Console.WriteLine();

        // Test with GPU (CUDA) - this is where crashes typically occur
        Console.WriteLine("  Step 3: Testing with GPU (CUDA enabled)...");
        Console.WriteLine("    This is where the crash typically occurs...");
        Console.WriteLine();
        
        var gpuResult = await RunWhisperCommand(cudaExePath, 
            $"-m \"{modelPath}\" -f \"{testWavPath}\" --no-timestamps", 
            verbose: true);
        
        if (gpuResult == 0)
        {
            PrintOk("  CUDA transcription succeeded!");
        }
        else
        {
            PrintError($"  CUDA transcription failed with exit code {gpuResult} (0x{gpuResult:X8})");
            
            if (gpuResult == -1073740791)
            {
                Console.WriteLine();
                PrintWarning(@"  STATUS_STACK_BUFFER_OVERRUN - CUDA version mismatch detected!

  The pre-built whisper.cpp CUDA binary was compiled against a specific
  cuBLAS version that doesn't match your installed CUDA toolkit.

  Your system:
  - CUDA Toolkit: Likely 11.8.0 (installed version)
  - Required: The specific patch version bundled in the whisper.cpp release

  Solutions (in order of ease):
  
  1. Use CPU mode - Already works, ~5-6 sec for 8-sec audio
     Set 'transcriptionBackend' to 1 (CpuOnly) in settings.json

  2. Copy CUDA DLLs from whisper.cpp release - The release ZIP contains
     compatible cuBLAS DLLs. Copy them to the whisper\cuda\Release folder.

  3. Build whisper.cpp from source - Guarantees compatibility:
     git clone https://github.com/ggerganov/whisper.cpp
     cmake -B build -DGGML_CUDA=ON
     cmake --build build --config Release");
            }
        }

        // Cleanup
        try { File.Delete(testWavPath); } catch { }

        return gpuResult;
    }

    private static async Task<int> RunCpuTest(string[] args)
    {
        PrintSection("CPU Backend Test");

        var modelName = args.FirstOrDefault() ?? "small.en";
        var modelPath = FindModel(modelName);

        if (modelPath == null)
        {
            PrintError($"Model not found: {modelName}");
            ListAvailableModels();
            return 1;
        }

        var cpuExePath = FindCpuExecutable();
        if (cpuExePath == null)
        {
            PrintError("CPU whisper executable not found");
            return 1;
        }

        Console.WriteLine($"  Executable: {cpuExePath}");
        Console.WriteLine($"  Model:      {modelPath}");
        Console.WriteLine();

        // Test model loading
        Console.WriteLine("  Testing model loading...");
        var result = await RunWhisperCommand(cpuExePath, $"-m \"{modelPath}\" --help", verbose: true);
        
        if (result == 0)
        {
            PrintOk("  CPU backend works correctly");
        }
        else
        {
            PrintError($"  CPU backend failed with exit code {result}");
        }

        return result;
    }

    private static async Task<int> RunTranscriptionTest(string[] args)
    {
        PrintSection("Transcription Test");

        var modelName = args.FirstOrDefault() ?? "small.en";
        var modelPath = FindModel(modelName);

        if (modelPath == null)
        {
            PrintError($"Model not found: {modelName}");
            ListAvailableModels();
            return 1;
        }

        // Create a test WAV file with silence
        var testWavPath = Path.Combine(Path.GetTempPath(), "voxtether-test.wav");
        Console.WriteLine($"  Creating test audio: {testWavPath}");
        CreateSilentWav(testWavPath, durationSeconds: 2);

        // Try CPU first
        var cpuExePath = FindCpuExecutable();
        if (cpuExePath != null)
        {
            Console.WriteLine();
            Console.WriteLine("  Testing CPU transcription...");
            var sw = Stopwatch.StartNew();
            var result = await RunWhisperCommand(
                cpuExePath, 
                $"-m \"{modelPath}\" -f \"{testWavPath}\" --no-timestamps", 
                verbose: true);
            sw.Stop();
            
            if (result == 0)
            {
                PrintOk($"  CPU transcription completed in {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                PrintError($"  CPU transcription failed");
            }
        }

        // Try CUDA
        var cudaExePath = FindCudaExecutable();
        if (cudaExePath != null)
        {
            Console.WriteLine();
            Console.WriteLine("  Testing CUDA transcription...");
            var sw = Stopwatch.StartNew();
            var result = await RunWhisperCommand(
                cudaExePath, 
                $"-m \"{modelPath}\" -f \"{testWavPath}\" --no-timestamps", 
                verbose: true);
            sw.Stop();
            
            if (result == 0)
            {
                PrintOk($"  CUDA transcription completed in {sw.ElapsedMilliseconds}ms");
            }
            else
            {
                PrintError($"  CUDA transcription failed with exit code {result}");
            }
        }

        // Cleanup
        try { File.Delete(testWavPath); } catch { }

        return 0;
    }

    private static async Task<int> RunWhisperDirect(string[] args)
    {
        if (args.Length == 0)
        {
            PrintError("Usage: voxtether-diag run <whisper-cli-args...>");
            return 1;
        }

        var cudaExePath = FindCudaExecutable() ?? FindCpuExecutable();
        if (cudaExePath == null)
        {
            PrintError("No whisper executable found");
            return 1;
        }

        Console.WriteLine($"  Executable: {cudaExePath}");
        Console.WriteLine($"  Arguments:  {string.Join(" ", args)}");
        Console.WriteLine();

        return await RunWhisperCommand(cudaExePath, string.Join(" ", args), verbose: true);
    }

    private static int ShowPaths()
    {
        PrintSection("VoxTether Paths");

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var paths = new Dictionary<string, string>
        {
            ["Settings"] = Path.Combine(appData, "VoxTether", "settings.json"),
            ["Logs"] = Path.Combine(appData, "VoxTether", "logs"),
            ["Models"] = Path.Combine(appData, "VoxTether", "models"),
            ["Whisper Base"] = Path.Combine(localAppData, "VoxTether", "whisper"),
            ["Whisper CPU"] = Path.Combine(localAppData, "VoxTether", "whisper", "cpu"),
            ["Whisper CUDA"] = Path.Combine(localAppData, "VoxTether", "whisper", "cuda"),
        };

        foreach (var (name, path) in paths)
        {
            var exists = File.Exists(path) || Directory.Exists(path);
            var status = exists ? $"{Green}✓{Reset}" : $"{Red}✗{Reset}";
            Console.WriteLine($"  {status} {name,-15}: {path}");
        }

        Console.WriteLine();

        // List whisper executables
        PrintSection("Whisper Executables Found");
        
        var whisperDir = paths["Whisper Base"];
        if (Directory.Exists(whisperDir))
        {
            var exeFiles = Directory.GetFiles(whisperDir, "*.exe", SearchOption.AllDirectories);
            foreach (var exe in exeFiles)
            {
                var relativePath = Path.GetRelativePath(whisperDir, exe);
                var fileInfo = new FileInfo(exe);
                Console.WriteLine($"    {relativePath,-40} ({fileInfo.Length / 1024:N0} KB)");
            }
        }
        else
        {
            PrintWarning($"  Whisper directory not found: {whisperDir}");
        }

        Console.WriteLine();

        // List models
        PrintSection("Models Found");
        ListAvailableModels();

        return 0;
    }

    // Helper methods

    private static async Task<int> RunWhisperCommand(string exePath, string arguments, bool verbose = false)
    {
        var execDir = Path.GetDirectoryName(exePath) ?? "";
        
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = execDir
        };

        // IMPORTANT: Prepend the executable's directory to PATH so local CUDA DLLs are loaded first
        // This fixes DLL loading issues where system PATH has incompatible CUDA versions
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        psi.EnvironmentVariables["PATH"] = $"{execDir};{currentPath}";

        try
        {
            using var process = Process.Start(psi);
            if (process == null)
            {
                PrintError("Failed to start process");
                return -1;
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    if (verbose)
                    {
                        Console.WriteLine($"    {e.Data}");
                    }
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                    if (verbose)
                    {
                        Console.WriteLine($"    {Yellow}{e.Data}{Reset}");
                    }
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = await Task.Run(() => process.WaitForExit(60000));

            if (!completed)
            {
                process.Kill();
                PrintError("Process timed out after 60 seconds");
                return -1;
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            PrintError($"Error running whisper: {ex.Message}");
            return -1;
        }
    }

    private static string? FindModel(string modelName)
    {
        if (_modelsPath == null) return null;

        // Add .bin extension if not present
        if (!modelName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            modelName = $"ggml-{modelName}.bin";
        }

        // Check if it's already a full path
        if (File.Exists(modelName))
            return modelName;

        // Check in models directory
        var modelPath = Path.Combine(_modelsPath, modelName);
        if (File.Exists(modelPath))
            return modelPath;

        // Try without ggml- prefix
        if (modelName.StartsWith("ggml-"))
        {
            modelPath = Path.Combine(_modelsPath, modelName[5..]);
            if (File.Exists(modelPath))
                return modelPath;
        }

        return null;
    }

    private static void ListAvailableModels()
    {
        if (_modelsPath == null || !Directory.Exists(_modelsPath))
        {
            PrintWarning($"  Models directory not found: {_modelsPath}");
            return;
        }

        var models = Directory.GetFiles(_modelsPath, "*.bin");
        if (models.Length == 0)
        {
            Console.WriteLine("  No models found");
            return;
        }

        foreach (var model in models)
        {
            var fileInfo = new FileInfo(model);
            Console.WriteLine($"    • {Path.GetFileName(model)} ({fileInfo.Length / 1024 / 1024:N0} MB)");
        }
    }

    private static string? FindCudaExecutable()
    {
        if (_whisperPath == null) return null;

        var searchPaths = new[]
        {
            Path.Combine(_whisperPath, "cuda", "Release", "whisper-cli.exe"),
            Path.Combine(_whisperPath, "cuda", "whisper-cli.exe"),
            Path.Combine(_whisperPath, "cuda", "Release", "main.exe"),
            Path.Combine(_whisperPath, "whisper_cuda.exe"),
        };

        return searchPaths.FirstOrDefault(File.Exists);
    }

    private static string? FindCpuExecutable()
    {
        if (_whisperPath == null) return null;

        var searchPaths = new[]
        {
            Path.Combine(_whisperPath, "cpu", "Release", "whisper-cli.exe"),
            Path.Combine(_whisperPath, "cpu", "whisper-cli.exe"),
            Path.Combine(_whisperPath, "cpu", "Release", "main.exe"),
            Path.Combine(_whisperPath, "whisper_cpu.exe"),
            Path.Combine(_whisperPath, "whisper-cli.exe"),
        };

        return searchPaths.FirstOrDefault(File.Exists);
    }

    private static void CreateSilentWav(string path, int durationSeconds)
    {
        // Create a minimal WAV file with silence
        // WAV format: 16-bit, 16000 Hz, mono (standard for whisper)
        const int sampleRate = 16000;
        const int bitsPerSample = 16;
        const int channels = 1;
        var numSamples = sampleRate * durationSeconds;
        var dataSize = numSamples * channels * (bitsPerSample / 8);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        // RIFF header
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataSize); // File size - 8
        writer.Write("WAVE"u8.ToArray());

        // fmt chunk
        writer.Write("fmt "u8.ToArray());
        writer.Write(16); // Chunk size
        writer.Write((short)1); // Audio format (PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8)); // Byte rate
        writer.Write((short)(channels * (bitsPerSample / 8))); // Block align
        writer.Write((short)bitsPerSample);

        // data chunk
        writer.Write("data"u8.ToArray());
        writer.Write(dataSize);

        // Write silent samples
        var silentSamples = new byte[dataSize];
        writer.Write(silentSamples);
    }

    private static string TruncatePath(string path, int maxLen)
    {
        if (path.Length <= maxLen) return path;
        return "..." + path[^(maxLen - 3)..];
    }

    private static int ShowBuildInstructions()
    {
        PrintSection("Building whisper.cpp from Source");
        
        Console.WriteLine($@"
The pre-built whisper.cpp CUDA binaries may not be compatible with your
specific CUDA toolkit installation. Building from source ensures ABI
compatibility with your system's CUDA libraries.

{Bold}Prerequisites:{Reset}
  • Visual Studio 2022 with C++ Desktop workload
  • CMake 3.20 or newer (https://cmake.org/download/)
  • CUDA Toolkit (you have 11.8.0 installed)
  • Git

{Bold}Step 1: Clone whisper.cpp{Reset}

  git clone https://github.com/ggerganov/whisper.cpp
  cd whisper.cpp

{Bold}Step 2: Configure with CUDA{Reset}
");

        // Detect CUDA path
        var cudaPath = FindCudaPath();
        var nvccPath = cudaPath != null ? Path.Combine(cudaPath, "bin", "nvcc.exe") : null;
        
        if (cudaPath != null && nvccPath != null && File.Exists(nvccPath))
        {
            Console.WriteLine($"  {Green}✓{Reset} CUDA Toolkit found at: {cudaPath}");
            Console.WriteLine($"  {Green}✓{Reset} nvcc compiler: {nvccPath}");
            Console.WriteLine();
            Console.WriteLine($"  cmake -B build -DGGML_CUDA=ON ^");
            Console.WriteLine($"    -DCMAKE_CUDA_COMPILER=\"{nvccPath}\" ^");
            Console.WriteLine($"    -DCMAKE_CUDA_ARCHITECTURES=89");
            Console.WriteLine();
            Console.WriteLine($"  {Yellow}Note:{Reset} Architecture 89 is for your RTX 4070 (Ada Lovelace)");
        }
        else
        {
            Console.WriteLine($"  {Yellow}⚠{Reset} CUDA Toolkit not found. Install from:");
            Console.WriteLine($"    https://developer.nvidia.com/cuda-toolkit-archive");
            Console.WriteLine();
            Console.WriteLine($"  cmake -B build -DGGML_CUDA=ON ^");
            Console.WriteLine($"    -DCMAKE_CUDA_COMPILER=\"path/to/nvcc.exe\" ^");
            Console.WriteLine($"    -DCMAKE_CUDA_ARCHITECTURES=89");
        }

        Console.WriteLine($@"
{Bold}Step 3: Build{Reset}

  cmake --build build --config Release

{Bold}Step 4: Install to VoxTether{Reset}

  Copy these files to: {_whisperPath}\cuda\Release\
  
  From build\bin\Release\ (or just build\bin\):
    • whisper-cli.exe
    • whisper.dll (or libwhisper.dll)
    • ggml.dll (or libggml.dll)
    • ggml-cuda.dll (or libggml-cuda.dll)
    • ggml-base.dll (or libggml-base.dll)
    • ggml-cpu.dll (or libggml-cpu.dll)

{Bold}Step 5: Test{Reset}

  voxtether-diag cuda

{Bold}Alternative: Use vcpkg (easier dependency management){Reset}

  If you have vcpkg installed, you can use it to manage dependencies:
  
  vcpkg install whisper-cpp[cuda]:x64-windows
");

        return 0;
    }

    private static string? FindCudaPath()
    {
        // Try environment variable
        var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
        if (!string.IsNullOrEmpty(cudaPath) && Directory.Exists(cudaPath))
            return cudaPath;

        // Try common installation paths
        var commonPaths = new[]
        {
            @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8",
            @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.0",
            @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.1",
            @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.2",
            @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.3",
            @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.4",
            @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.5",
            @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.6",
        };

        foreach (var path in commonPaths)
        {
            if (Directory.Exists(path))
                return path;
        }

        return null;
    }

    private static string BoolStatus(bool value) => 
        value ? $"{Green}Yes{Reset}" : $"{Yellow}No{Reset}";

    private static void PrintSection(string title)
    {
        Console.WriteLine($"{Bold}{Cyan}▶ {title}{Reset}");
        Console.WriteLine();
    }

    private static void PrintOk(string message) =>
        Console.WriteLine($"  {Green}✓{Reset} {message}");

    private static void PrintError(string message) =>
        Console.WriteLine($"  {Red}✗{Reset} {message}");

    private static void PrintWarning(string message) =>
        Console.WriteLine($"  {Yellow}⚠{Reset} {message}");

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    private static void EnableAnsiColors()
    {
        try
        {
            var handle = GetStdHandle(-11); // STD_OUTPUT_HANDLE
            if (GetConsoleMode(handle, out uint mode))
            {
                SetConsoleMode(handle, mode | 0x0004); // ENABLE_VIRTUAL_TERMINAL_PROCESSING
            }
        }
        catch
        {
            // Ignore if we can't enable ANSI colors
        }
    }
}
