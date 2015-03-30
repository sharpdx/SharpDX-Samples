This directory contains some samples directly ported from the [Windows 8.1 Samples SDK](http://code.msdn.microsoft.com/windowsapps/Windows-8-Modern-Style-App-Samples) to SharpDX.

Each C#/SharpDX sample is trying to strictly follow as much as possible the original C++ sample, in order to provide a one-to-one translation between C++ and C#. More specifically, you will learn:
 - **How to create Direct3D objects**: in C# we are using standard constructors but in C++ they are using factories
 - **How to query COM interface**: in C# with SharpDX, we are using QueryInterface<T>, similar to C++
 - **How to release COM objects**: in C#, we are using explicitly the using/Dispose() pattern, while in C++ smart pointers are used and implicitly releasing references. 
 - **How to access native file without async **: in C# with SharpDX, we are using `NativeFile`/`NativeFileStream` API similar in C++.

The following samples have been ported:

 - XAML SurfaceImageSource DirectX interop sample
 - ... (TODO)


## Old samples

The following samples are old Windows 8 samples. They will be removed in a future release.  

> Disclaimer
> - This samples were ported quickly on Windows 8 launch and thus are not in a production quality state.
> - The Dispose() of allocated COM objects is not always fully performed
> - The resize event is not handled
> - The suspend app is not handled
> - The CommonDX cannot be considered as a framework. Do not use it for a production quality application.

- D2DCustomPixelShaderEffect
- D2DCustomVertexShaderEffect
- D2DEffectsHelloWorld
- D2DLightingEffects
- MediaPlayerApp
- MiniCube
- MiniCubeBrushXaml
- MiniCubeTexture
- MiniCubeVideoTexture
- MiniCubeXaml
- MiniShape
- SimpleMiniCube
