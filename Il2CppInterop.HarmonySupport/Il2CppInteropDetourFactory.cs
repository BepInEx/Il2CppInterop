using System.Diagnostics.CodeAnalysis;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using MonoMod.Core;

namespace Il2CppInterop.HarmonySupport;

public sealed class Il2CppInteropDetourFactory(IDetourFactory? fallback = null) : IDetourFactory
{
    private readonly IDetourFactory _fallback = fallback ?? DetourFactory.Current;

    public ICoreDetour CreateDetour(CreateDetourRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Target);

        if (TryCreateDetour(request, out var detour))
        {
            return detour;
        }

        return _fallback.CreateDetour(request);
    }

    private static bool TryCreateDetour(CreateDetourRequest request, [NotNullWhen(true)] out Il2CppInteropDetour? detour)
    {
        var declaringType = request.Source.DeclaringType;
        if (declaringType != null && Il2CppType.From(declaringType, false) != null && !ClassInjector.IsManagedTypeInjected(declaringType))
        {
            if (!request.CreateSourceCloneIfNotILClone)
            {
                throw new InvalidOperationException("IDetourFactory consumer has to support CreateSourceCloneIfNotILClone");
            }

            var methodField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(request.Source);

            if (methodField == null)
            {
                var fieldInfoField = Il2CppInteropUtils.GetIl2CppFieldInfoPointerFieldForGeneratedFieldAccessor(request.Source);

                if (fieldInfoField != null)
                {
                    throw new Exception($"Method {request.Source} is a field accessor, it can't be patched.");
                }

                // Generated method is probably unstripped, it can be safely handed to IL handler
                detour = null;
                return false;
            }

            var nativeSourcePointer = methodField.GetValue(null) ?? throw new Exception();
            INativeMethodInfoStruct nativeSource;
            unsafe
            {
                nativeSource = UnityVersionHandler.Wrap((Il2CppMethodInfo*)(IntPtr)nativeSourcePointer);
            }

            detour = new Il2CppInteropDetour(request.Source, nativeSource, request.Target);
            if (request.ApplyByDefault)
            {
                detour.Apply();
            }

            return true;
        }

        detour = null;
        return false;
    }

    public ICoreNativeDetour CreateNativeDetour(CreateNativeDetourRequest request)
    {
        return _fallback.CreateNativeDetour(request);
    }

    public bool SupportsNativeDetourOrigEntrypoint => true;
}
