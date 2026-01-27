# Best Open-Source Speech-to-Text Models for .NET (Real-Time, GPU-Enabled)

When building an offline speech transcription feature in a .NET app (Windows, NVIDIA RTX 4070), several open-source ASR models stand out. Below we detail the top options, focusing on real-time performance, GPU acceleration, .NET integration, and a balance of latency vs accuracy. Each model description includes integration methods, GPU support, performance characteristics, and setup notes.

---

## OpenAI Whisper (via Whisper.cpp & .NET bindings)

OpenAI's Whisper is a state-of-the-art speech recognition model trained on 680k hours of multilingual data. It comes in multiple sizes (Tiny, Base, Small, Medium, Large) offering a trade-off between speed and accuracy – smaller models run faster with less accuracy, larger ones are more accurate but slower. Whisper achieves near human-level robustness, handling accents, background noise, and technical language well. Its open-source code and models (MIT license) make it suitable for local, real-time use on high-end GPUs.

### Integration with .NET

The easiest path is using the Whisper.cpp C++ port along with a .NET wrapper. Notably, the **Whisper.net** library provides .NET bindings for Whisper.cpp. It's available as a NuGet package (`Whisper.net.AllRuntimes`), which bundles CPU and GPU backends. You can install it and call Whisper from C# directly.

Another high-performance option is the **Const-me/Whisper** project, which is a Windows-focused Whisper.cpp fork using Direct3D11 compute shaders; it offers a COM-style API with an idiomatic C# NuGet package. This means you can integrate Whisper into a .NET app without running Python – just add the NuGet and use the provided C# API to transcribe audio.

### GPU Acceleration

**Yes** – optimized for NVIDIA GPUs. Whisper.net includes native binaries with CUDA support (both CUDA 13 and 12) for NVIDIA GPUs. On a system with an RTX GPU, you can leverage CUDA cores to accelerate inference. (Prerequisites include the NVIDIA CUDA Toolkit and VS2015+ C++ runtime on Windows.)

The Const-me/Whisper library uses DirectCompute (DirectX 11) for vendor-agnostic GPU acceleration – it will run on any DirectX11-capable GPU, including the RTX 4070, without needing CUDA. In short, you can fully utilize the RTX 4070 for Whisper inference.

### Performance (Latency & Accuracy)

Whisper's accuracy is state-of-the-art – the Large model achieves very low word error rates on English (OpenAI reports SOTA WER on LibriSpeech and competitive results across many languages). In exchange, the Large model is heavy (~1.5 billion params).

For a middle ground between latency and accuracy, the **Medium or Small models** are recommended. For example, the Medium model (approx 769 MB) strikes a good balance and with GPU acceleration, it can transcribe in real-time or faster on high-end GPUs.

The custom DirectCompute implementation shows a 3.4-minute audio transcribing in ~19 seconds on a GeForce 1080 Ti using the Medium model, compared to 45 seconds with the original PyTorch+CUDA code. That's ~10× faster than real-time on a 1080 Ti; an RTX 4070 (which is more powerful) can similarly handle Medium or even Large model in real-time.

Smaller models (Small/Base) run even quicker (several times real-time on GPU), at the cost of some accuracy on complex speech. In practice, Whisper Small or Medium on an RTX 4070 should yield low latency (a few hundred milliseconds for streaming chunks) with excellent accuracy for personal use.

Whisper supports segmenting streaming audio; while it doesn't natively output word-by-word streaming results, you can process audio in small chunks (e.g. 5-second windows) to achieve near-real-time transcription. Many community projects extend Whisper for streaming mode.

### Setup Notes

Using Whisper.net, you would download the desired Whisper model (OpenAI provides `.bin` or `.ggml` files – e.g. `ggml-medium.bin` for Medium) and place it in your app or have Whisper.net download it. Install the NuGet packages for `Whisper.net` core and your chosen runtime (CPU, Cuda, etc.). Ensure the CUDA toolkit (if using CUDA backend) is installed and that your GPU drivers are up to date.

With Const-me/Whisper, you can use their pre-built `Whisper.dll` and C# wrapper (available on NuGet as `WhisperNet` package). Both Whisper.net and Const-me's library are well-documented on their GitHub pages, with examples (e.g., using NAudio to stream microphone audio into Whisper). In code, you'll initialize the model, then feed audio buffers (PCM float or WAV) and receive transcribed text.

### Repository/Documentation

- [OpenAI Whisper GitHub](https://github.com/openai/whisper)
- [Whisper.net GitHub](https://github.com/sandrohanea/whisper.net)
- [Const-me/Whisper GitHub](https://github.com/Const-me/Whisper)

Both projects are active and maintained. In summary, OpenAI Whisper (with these integration libraries) offers top-notch accuracy with GPU-accelerated real-time transcription in .NET.

---

## Vosk Speech Recognition Toolkit

Vosk is an offline speech recognition toolkit based on Kaldi. It's known for being lightweight and efficient, running in real-time even on CPUs, and supports over 20 languages out-of-the-box. Vosk's models are relatively small (e.g. ~50 MB for some English models) yet provide continuous large vocabulary transcription with streaming, low-latency output. This makes Vosk a strong choice when you need quick transcription with minimal resources.

### Integration with .NET

Vosk offers official bindings for multiple languages, including C#/.NET. There is a NuGet package ("Vosk", version 0.3.x) that targets .NET Standard 2.0, meaning you can install it in .NET 6/7/8 projects easily.

Using the C# API is straightforward – you load a Vosk acoustic model (from a folder or zip), then create a recognizer object and feed it audio stream chunks. Vosk's streaming API allows you to get partial results as you send audio in (useful for real-time transcription from a live mic). Because Vosk's backend is in C/C++ (Kaldi), under the hood the .NET package p/invokes a native library, but all that complexity is hidden – you interact with simple C# classes.

### GPU Acceleration

**Not required (CPU-based).** Vosk is optimized for CPUs and even embedded devices; it does not rely on a GPU for inference in typical usage. On an NVIDIA RTX 4070, Vosk will still run on the CPU (e.g., using vectorized CPU instructions). This is by design – models are small enough and Kaldi's inference is efficient enough to achieve real-time on modern CPUs.

There is no official CUDA or Tensor core utilization for Vosk (Kaldi can be compiled with CUDA for neural net components, but the default Vosk packages don't mandate it). The upside is simpler deployment (no CUDA toolkit needed), though it means the RTX 4070 isn't doing the heavy lifting. In practice, Vosk can transcribe 16kHz speech in real-time on a standard PC CPU core, so GPU acceleration isn't crucial here.

### Performance (Latency & Accuracy)

Vosk provides **low latency streaming transcription** – it can start returning partial text almost immediately as you speak. Its end-to-end latency is essentially the audio chunk size you choose (often a few hundred milliseconds).

Vosk's accuracy is good for many tasks but not as high as newer deep learning models like Whisper. It uses Kaldi-derived acoustic models (typically chain models) with a fixed vocabulary language model. For clear speech, you might get word error rates in the ~10-15% range (depending on the model and domain). The models are also customizable with limited vocabulary or grammar if needed, which can boost accuracy in constrained contexts.

In summary, accuracy is decent (sufficient for many personal projects, command-and-control, dictation with clear speech) but if you need cutting-edge accuracy on open-domain speech, Whisper or other large models outperform it. The benefit is that Vosk's smallest English model (40MB) can even run on a Raspberry Pi, and larger models (e.g. 1.8GB multilingual) improve accuracy if needed.

For a middle-ground use-case on a PC, Vosk's latency is excellent (virtually zero lag streaming), and accuracy is acceptable for personal use, especially in clean acoustic conditions.

### Setup Notes

After adding the Vosk NuGet package, you need to obtain a pretrained model file for your language. The Vosk website provides model packs (for English, and other languages) on their [models page](https://alphacephei.com/vosk/models). For example, `vosk-model-small-en-us-0.15` is ~50 MB and suitable for real-time English transcription. Download and unzip the model in your app directory.

In code, initialize the recognizer with the path to the model folder. Ensure your audio is 16 kHz mono PCM (you may use NAudio or similar to capture and resample microphone input). Vosk's recognizer will output JSON results (with text and word timing) which you can parse for the transcript.

Since Vosk runs on CPU, no special drivers or GPU libraries are needed. Just make sure to target x64 (as the native lib might be 64-bit) and redistribute the Vosk native DLL that comes with the NuGet if needed. Documentation and examples for .NET usage can be found in the Vosk repository and Wiki – the maintainers provide simple C# demo code.

### Repository/Docs

- [Vosk GitHub (alphacep/vosk-api)](https://github.com/alphacep/vosk-api)
- [Vosk Model Downloads](https://alphacephei.com/vosk/models)
- [NuGet Package](https://www.nuget.org/packages/Vosk/)

Vosk's active community and multi-language support make it a solid choice when Whisper's large models are impractical or when GPU usage is not possible.

---

## Coqui STT (DeepSpeech Fork)

Coqui STT is the open-source successor to Mozilla's DeepSpeech, a deep-learning ASR model inspired by Baidu's DeepSpeech architecture. It provides a pre-trained English model and supports training your own. Coqui STT is designed for streaming and real-time inference and has a relatively small footprint compared to massive transformer models. It's a good middle-ground solution: more modern than Kaldi/Vosk (being a neural end-to-end model), but lighter-weight than Whisper.

### Integration with .NET

Coqui STT (and earlier Mozilla DeepSpeech) comes with a native C++ inference library and bindings for multiple languages. In .NET, you can use the DeepSpeech .NET bindings that were provided in the Mozilla era. For example, Mozilla's DeepSpeech 0.9 documentation included a C# example for streaming inference (there was a NuGet package `DeepSpeech 0.9.x`).

Coqui STT has continued that approach; you typically install the Coqui STT Python package or use their native client. For .NET, one approach is to use P/Invoke with the `libdeepspeech.so`/`libstt.dll` (the native client library) – there have been community wrappers and possibly updated NuGet packages (e.g., a Coqui STT .NET package may exist, or one can use the older DeepSpeech C# package with Coqui's model).

While not as plug-and-play as Whisper.net, it's feasible to call Coqui's C API from C# by importing functions like `STT_CreateModel`, `STT_SpeechToText`, etc. Another integration strategy is to run Coqui STT in a Python subprocess and communicate via gRPC or STDIN/STDOUT, but in-process usage via the native library will yield better performance.

### GPU Acceleration

**Optional, but not typical for inference in .NET.** Coqui STT's training pipeline supports GPUs (multi-GPU training, since it's built on TensorFlow/PyTorch), but for inference the provided native client is optimized for CPU (using TensorFlow Lite or similar under the hood for efficiency). The official runtime is primarily CPU-bound.

There was a TensorFlow-based "deepspeech-gpu" package in the past which could use CUDA for inference, but that would require the Python environment and the full TensorFlow, which isn't trivial to embed in .NET. For a .NET app, you'll likely use the TFLite-based engine on CPU, which is fast enough to be real-time for one audio stream on a modern CPU.

In short, your RTX 4070 won't be utilized by Coqui's default inference library. Given that Coqui's acoustic model is a few hundred MB and uses an RNN/beam search decoder, it can run at or near real-time on a CPU core.

### Performance (Latency & Accuracy)

Coqui's STT model offers **real-time transcription** – it was designed to run on anything from high-end GPUs to a Raspberry Pi 4 in real-time. In practice, on a PC CPU you can expect realtime or faster-than-real-time processing for 16 kHz speech. It also supports streaming inference, meaning you can feed audio incrementally and get partial results as it listens (DeepSpeech-based models produce text as the audio comes in, using an RNN transducer/CTC approach with a beam search).

Accuracy is good, though not cutting-edge. The English model (trained on ~1700 hours) reportedly reached about ~7-10% WER on benchmark datasets. This is comparable to mid-tier systems: better than older Kaldi models (and possibly on par with Whisper Tiny/Base on some tasks), but not as strong as Whisper Large.

For dictated speech or read speech, Coqui STT does fairly well. It may struggle more with very spontaneous speech or heavy accents compared to Whisper. However, because it uses an external language model (KenLM) for decoding, you can customize the lexicon or bias it to your domain, which can improve accuracy for specific vocabulary.

Latency is low – as an RNN, it outputs text with a slight delay (it may wait until a word is finished or a short silence to finalize the text). In summary, **accuracy-medium, latency-low**: a viable middle-ground.

### Setup Instructions

To use Coqui STT, first download the pre-trained model from Coqui's releases (e.g., `coqui-stt-english-v1.0.0.tflite` or `.pbmm` model file, plus an accompanying scorer or alphabet file). Then, either use the Coqui STT .NET wrapper if available, or use the C# P/Invoke route.

If using the legacy DeepSpeech 0.9 .NET NuGet, note that you should load Coqui's model into it – the Coqui model is backward compatible with DeepSpeech's runtime (Coqui started from DeepSpeech 0.9). Include the native client library: Coqui provides a DLL for Windows in their GitHub releases. Make sure the DLL is accessible (e.g., in your output directory or in a known PATH).

In code, you'll create a Model object, call something like `Model.SpeechToText()` on audio buffers, or use the streaming API (`StartStream`, `FeedAudioContent`, etc. then `FinishStream`). The Coqui STT documentation (on stt.readthedocs.io) has instructions on setting up on different platforms, and the GitHub has examples. You might need the Microsoft VC++ redistributable if the native client depends on it. No special GPU driver setup is required for the default use.

### Repository/Documentation

- [Coqui STT GitHub](https://github.com/coqui-ai/STT)
- [Coqui STT Docs](https://stt.readthedocs.io/)
- [DeepSpeech 0.9 .NET Documentation](https://deepspeech.readthedocs.io/en/r0.9/DotNet-API.html)
- [SourceForge Mirror](https://sourceforge.net/projects/coqui-stt.mirror/)

The project is maintained and the community is active on Coqui's forums. If your focus is an open-source engine that's proven in production (DeepSpeech was used in many projects) and easy to integrate with custom code, Coqui STT is a solid choice.

---

## Additional Options and Tools

Beyond the above three, there are a few other notable mentions:

### Hugging Face Wav2Vec2 & Other Models via ONNX

Meta's Wav2Vec 2.0 and similar transformer models (e.g., Facebook's wav2vec2, XLS-R for multilingual, etc.) are open-source and achieve high accuracy. While typically used through Python, you can convert these models to ONNX and run them in .NET using Microsoft's ONNX Runtime (which supports GPU execution).

For instance, an English Wav2Vec2 model fine-tuned on 960h can be exported and run with ONNX Runtime's CUDA or DirectML providers. Integration would involve using the `OnnxRuntime` NuGet in .NET and feeding audio to the model, then implementing a decoder (CTC beam search) to get text.

This approach can be complex (especially writing a beam search in .NET and handling model I/O), so it's recommended for advanced users. However, it offers flexibility – virtually any ASR model on Hugging Face could be used this way with GPU acceleration. There are community projects like Whisper ONNX and [EchoSharp.Onnx](https://github.com/sandrohanea/echosharp) that experiment with this.

If you prefer using standard ML frameworks in .NET, you might also consider TorchSharp (a .NET wrapper for LibTorch) to load and run a PyTorch ASR model on the GPU.

### NVIDIA NeMo / Riva ASR

NVIDIA's NeMo toolkit provides cutting-edge ASR models (Jasper, QuartzNet, Citrinet, Conformer-Transducer, etc.), some of which are available pre-trained. These models can often match Whisper's accuracy on English. NeMo models are primarily used in Python, but importantly most NeMo models can be [exported to ONNX](https://docs.nvidia.com/nemo-framework/user-guide/24.07/nemotoolkit/core/export.html). Once in ONNX, you can run them in .NET (again via ONNX Runtime).

NVIDIA also offers Riva, a GPU-accelerated speech server that includes ASR microservices for real-time transcription (optimized for RTX GPUs). Riva isn't fully open-source (it's free for development but requires NVIDIA's containers), and it runs as a separate server (Linux container or WSL) rather than embedding into your .NET process.

Still, for completeness: if ultra-low-latency streaming and enterprise-grade performance are needed, one could deploy Riva on the RTX 4070 and call it from .NET via gRPC. For a purely open-source route, using NeMo models via ONNX is viable. For example, you could take NeMo's Conformer-CTC Large model, export to ONNX, and use the `Microsoft.ML.OnnxRuntime` GPU backend to transcribe audio in real-time on Windows. The integration effort is higher (you must manage audio streaming and text decoding), so this is a secondary option if the above easier solutions don't meet your needs.

### Sherpa-ONNX

[Sherpa-ONNX](https://k2-fsa.github.io/sherpa/onnx/index.html) is an open-source runtime focused on streaming ASR with minimal latency, developed by the Kaldi/K2 team. It packs efficient implementations of various models (including transducer models, Paraformer, and even Whisper) into a single library with bindings for C#, Java, etc.

Sherpa-ONNX comes with pre-trained models (e.g., a streaming Zipformer transducer model for English and Chinese) and can utilize ONNX Runtime CUDA on Windows. It has a C# API, so you can integrate it similarly to Vosk or Whisper.net. This project is very actively maintained.

If you require true live transcription (word-by-word, low latency) and are willing to use a pre-packaged solution, Sherpa-ONNX is worth exploring. It essentially handles the model inference and decoding internally, exposing a simple API to apps. Setup involves downloading the Sherpa-ONNX binaries for Windows (available with GPU support) and one of the provided model files (like a Zipformer model). Then you call the C# API to start streaming from a microphone.

This is more specialized, but it's arguably state-of-the-art in streaming ASR. The downside is less community familiarity compared to Whisper or Vosk.

---

## Summary

For a personal .NET application on Windows with an RTX 4070, the **OpenAI Whisper** model (using .NET integrations like Whisper.net or Const-me's Whisper library) is a top choice, balancing high accuracy with reasonable latency when using a mid-sized model on GPU. It's actively maintained and easy to drop into .NET.

**Vosk** is another reliable choice if you favor simplicity and CPU operation; it offers quick, streaming results and easy .NET integration (though with moderate accuracy).

**Coqui STT (DeepSpeech)** provides a middle ground as well, with proven real-time capability and an open model, albeit slightly older tech.

Meanwhile, advanced users can explore ONNX-based approaches (Whisper ONNX, Wav2Vec2, or NeMo models) or frameworks like Sherpa-ONNX for streaming needs.

All these options are open-source and have permissive licenses:
- Whisper: MIT
- Vosk: Apache 2.0
- DeepSpeech/Coqui: MPL 2.0
- NeMo: Apache 2.0

This is suitable for personal/non-commercial projects. The key is to choose the model that meets your accuracy needs while still running in real-time on your hardware – with an RTX 4070 at hand, you have the freedom to use heavier models like Whisper Medium/Large and still achieve low latency transcription.

---

## Quick Reference Links

| Solution | Repository | Documentation |
|----------|------------|---------------|
| **Whisper** | [OpenAI Whisper](https://github.com/openai/whisper) | [Whisper.net](https://github.com/sandrohanea/whisper.net), [Const-me/Whisper](https://github.com/Const-me/Whisper) |
| **Vosk** | [Vosk GitHub](https://github.com/alphacep/vosk-api) | [Model Downloads](https://alphacephei.com/vosk/models), [NuGet](https://www.nuget.org/packages/Vosk/) |
| **Coqui STT** | [Coqui STT GitHub](https://github.com/coqui-ai/STT) | [STT Docs](https://stt.readthedocs.io/), [DeepSpeech .NET](https://deepspeech.readthedocs.io/en/r0.9/DotNet-API.html) |
| **NeMo/Riva** | [NVIDIA NeMo](https://github.com/NVIDIA/NeMo) | [Export Guide](https://docs.nvidia.com/nemo-framework/user-guide/24.07/nemotoolkit/core/export.html), Riva Docs |
| **Sherpa-ONNX** | [Sherpa-ONNX GitHub](https://github.com/k2-fsa/sherpa-onnx) | [Sherpa Docs](https://k2-fsa.github.io/sherpa/onnx/index.html) |

Using these resources, you should be able to integrate a real-time speech-to-text capability into your .NET application that fully leverages your RTX GPU for fast and accurate transcription.
