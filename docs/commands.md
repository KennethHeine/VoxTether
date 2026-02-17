# VoxTether Quick Commands

This document provides quick one-liner commands for developers.

## Start Frontend Locally

```powershell
cd src/frontend-electron; npm install; npm start
```

## Start Backend Locally

See [VoxTether-backend](https://github.com/KennethHeine/VoxTether-backend) for backend commands.

## Frontend Quality Checks

```powershell
cd src/frontend-electron; npm run lint
cd src/frontend-electron; npm test
```

For Linux CI/headless environments:

```bash
cd src/frontend-electron && xvfb-run --auto-servernum npm test
```

## Frontend Build Commands

```powershell
cd src/frontend-electron; npm run build
cd src/frontend-electron; npm run pack
```

## Frontend Test Variants

```powershell
cd src/frontend-electron; npm run test:ui
cd src/frontend-electron; npm run test:headed
```
