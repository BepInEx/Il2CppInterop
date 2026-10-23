using System;
using System.Linq;
using System.Runtime.InteropServices;
using Iced.Intel;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Extensions;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Class;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.FieldInfo;
using Il2CppInterop.Runtime.Startup;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection.Hooks
{
    internal unsafe class Class_GetFieldDefaultValue_Hook : Hook<Class_GetFieldDefaultValue_Hook.MethodDelegate>
    {
        public override string TargetMethodName => "Class::GetDefaultFieldValue";
        public override MethodDelegate GetDetour() => Hook;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate byte* MethodDelegate(Il2CppFieldInfo* field, out Il2CppTypeStruct* type);

        private byte* Hook(Il2CppFieldInfo* field, out Il2CppTypeStruct* type)
        {
            if (EnumInjector.GetDefaultValueOverride(field, out IntPtr newDefaultPtr))
            {
                INativeFieldInfoStruct wrappedField = UnityVersionHandler.Wrap(field);
                INativeClassStruct wrappedParent = UnityVersionHandler.Wrap(wrappedField.Parent);
                INativeClassStruct wrappedElementClass = UnityVersionHandler.Wrap(wrappedParent.ElementClass);
                type = wrappedElementClass.ByValArg.TypePointer;
                return (byte*)newDefaultPtr;
            }
            return Original(field, out type);
        }

        private static readonly MemoryUtils.SignatureDefinition[] s_Signatures =
        {
            // Test Game - Unity 2021.3.4 (x64)
            new MemoryUtils.SignatureDefinition
            {
                pattern = "\x48\x89\x5C\x24\x08\x48\x89\x74\x24\x10\x57\x48\x83\xEC\x20\x48\x8B\x79\x10\x48\x8B\xD9\x48\x8B\xF2\x48\x2B\x9F",
                mask = "xxxxxxxxxxxxxx?xxxxxxxxxxxxx",
                xref = false
            },
            // V Rising - Unity 2022.3.23 (x64)
            new MemoryUtils.SignatureDefinition
            {
                pattern = "\x48\x89\x5C\x24\x08\x48\x89\x74\x24\x10\x57\x48\x83\xEC\x40\x48\x8B\x41\x10",
                mask = "xxxxxxxxxxxxxxxxxxx",
                xref = false
            },
            // GTFO - Unity 2019.4.21 (x64)
            // DigitalCraft Dolce 3.1.2 - Unity 2022.3.55 (x64)
            new MemoryUtils.SignatureDefinition
            {
                pattern = "\x48\x89\x5C\x24\x08\x57\x48\x83\xEC\x20\x48\x8B\x41\x10\x48\x8B\xD9\x48\x8B",
                // mask the stack allocation length
                mask = "xxxxxxxxx?xxxxxxxxx",
                xref = false
            },
            // Idle Slayer - Unity 2021.3.17 (x64)
            new MemoryUtils.SignatureDefinition
            {
                pattern = "\x40\x53\x48\x83\xEC\x20\x48\x8B\xDA\xE8\x00\x00\x00\x00\x4C\x8B\xC8\x48\x85\xC0",
                mask = "xxxxxxxxxx????xxxxxx",
                xref = false
            },
            // Evony - Unity 2018.4.0 (x86)
            new MemoryUtils.SignatureDefinition
            {
                pattern = "\x55\x8B\xEC\x56\xFF\x75\x08\xE8\x00\x00\x00\x00\x8B\xF0\x83\xC4\x04\x85\xF6",
                mask = "xxxxxxxx????xxxxxxx",
                xref = false
            },
            // Idle Slayer - Unity 2021.3.23 (x64)
            new MemoryUtils.SignatureDefinition
            {
                pattern = "\x40\x53\x48\x83\xEC\x20\x48\x8B\xDA\xE8\xCC\xCC\xCC\xCC\x4C",
                mask = "xxxxxxxxxx????x",
                xref = false
            }
        };

        private static nint FindClassGetFieldDefaultValueXref(bool forceICallMethod = false)
        {
            nint classGetDefaultFieldValue = 0;
            if (forceICallMethod)
            {
                // MonoField isn't present on 2021.2.0+
                var monoFieldType = InjectorHelpers.Il2CppMscorlib.GetTypesSafe().SingleOrDefault((x) => x.Name is "MonoField");
                if (monoFieldType == null)
                    throw new Exception($"Unity {Il2CppInteropRuntime.Instance.UnityVersion} is not supported at the moment: MonoField isn't present in Il2Cppmscorlib.dll for unity version, unable to fetch icall");

                var monoFieldGetValueInternalThunk = InjectorHelpers.GetIl2CppMethodPointer(monoFieldType.GetMethod(nameof(Il2CppSystem.Reflection.MonoField.GetValueInternal)));
                Logger.Instance.LogTrace("Il2CppSystem.Reflection.MonoField::thunk_GetValueInternal: 0x{MonoFieldGetValueInternalThunkAddress}", monoFieldGetValueInternalThunk.ToInt64().ToString("X2"));

                var monoFieldGetValueInternal = XrefScannerLowLevel.JumpTargets(monoFieldGetValueInternalThunk).Single();
                Logger.Instance.LogTrace("Il2CppSystem.Reflection.MonoField::GetValueInternal: 0x{MonoFieldGetValueInternalAddress}", monoFieldGetValueInternal.ToInt64().ToString("X2"));

                // Field::GetValueObject could be inlined with Field::GetValueObjectForThread
                var fieldGetValueObject = XrefScannerLowLevel.JumpTargets(monoFieldGetValueInternal).Single();
                Logger.Instance.LogTrace("Field::GetValueObject: 0x{FieldGetValueObjectAddress}", fieldGetValueObject.ToInt64().ToString("X2"));

                var fieldGetValueObjectForThread = XrefScannerLowLevel.JumpTargets(fieldGetValueObject).Last();
                Logger.Instance.LogTrace("Field::GetValueObjectForThread: 0x{FieldGetValueObjectForThreadAddress}", fieldGetValueObjectForThread.ToInt64().ToString("X2"));

                classGetDefaultFieldValue = XrefScannerLowLevel.JumpTargets(fieldGetValueObjectForThread).ElementAt(2);
            }
            else
            {
                var getStaticFieldValueAPI = InjectorHelpers.GetIl2CppExport(nameof(IL2CPP.il2cpp_field_static_get_value));
                Logger.Instance.LogTrace("il2cpp_field_static_get_value: 0x{GetStaticFieldValueApiAddress}", getStaticFieldValueAPI.ToInt64().ToString("X2"));

                var getStaticFieldValue = XrefScannerLowLevel.JumpTargets(getStaticFieldValueAPI).Single();
                Logger.Instance.LogTrace("Field::StaticGetValue: 0x{GetStaticFieldValueAddress}", getStaticFieldValue.ToInt64().ToString("X2"));

                var getStaticFieldValueTargets = XrefScannerLowLevel.JumpTargets(getStaticFieldValue).ToList();

                // Sometimes the compiler can do an optimization and omit 'retn' instruction,
                // which then causes code following to grab wrong function pointer. A correct match should not contain more than 4 jumps
                // This optimization also causes Field::StaticGetValueInternal method to be located right under Field::StaticGetValue method
                // Example: https://discord.com/channels/623153565053222947/754333645199900723/1104817647171932283
                if (getStaticFieldValueTargets.Count > 4)
                    return getStaticFieldValueTargets[^2];

                var getStaticFieldValueInternal = getStaticFieldValueTargets[^1];
                Logger.Instance.LogTrace("Field::StaticGetValueInternal: 0x{GetStaticFieldValueInternalAddress}", getStaticFieldValueInternal.ToInt64().ToString("X2"));

                var getStaticFieldValueInternalTargets = XrefScannerLowLevel.JumpTargets(getStaticFieldValueInternal).ToArray();

                if (getStaticFieldValueInternalTargets.Length == 0) return FindClassGetFieldDefaultValueXref(true);

                // Clang lays the literal-field branch out as Class::GetDefaultFieldValue(field, &type) followed by
                // BlobReader::GetConstantValueFromBlob, so the position-based choice below picks the wrong call.
                // The call that receives the address of a stack slot (the 'type' out parameter) is the one we want.
                if (!OperatingSystem.IsWindows())
                {
                    var callWithOutArgument = FindCallWithStackAddressInSecondArgument(getStaticFieldValueInternal);
                    if (callWithOutArgument != 0)
                        return callWithOutArgument;
                }

                classGetDefaultFieldValue = getStaticFieldValueInternalTargets.Length == 3 ? getStaticFieldValueInternalTargets.Last() : getStaticFieldValueInternalTargets.First();
            }
            return classGetDefaultFieldValue;
        }

        /// <summary>
        /// The target of the only direct call in <paramref name="function"/> (up to its first return) whose second
        /// System V argument (rsi) was just set to the address of a stack slot (lea or mov from rsp/rbp); 0 if there is none or more than one.
        /// </summary>
        private static nint FindCallWithStackAddressInSecondArgument(nint function)
        {
            var decoder = XrefScanner.DecoderForAddress(function);
            nint found = 0;
            var secondArgumentIsStackAddress = false;
            while (true)
            {
                decoder.Decode(out var instruction);
                if (decoder.LastError == DecoderError.NoMoreBytes || instruction.Mnemonic == Mnemonic.Int3 ||
                    instruction.FlowControl == FlowControl.Return)
                    break;

                if (instruction.FlowControl == FlowControl.Call)
                {
                    if (secondArgumentIsStackAddress && instruction.Op0Kind is OpKind.NearBranch32 or OpKind.NearBranch64)
                    {
                        if (found != 0)
                            return 0;
                        found = (nint)instruction.NearBranchTarget;
                    }
                    secondArgumentIsStackAddress = false;
                }
                else if (instruction.Mnemonic == Mnemonic.Lea && instruction.Op0Register == Register.RSI)
                {
                    secondArgumentIsStackAddress = instruction.MemoryBase is Register.RSP or Register.RBP;
                }
                else if (instruction.Mnemonic == Mnemonic.Mov && instruction.Op0Register == Register.RSI &&
                         instruction.Op1Kind == OpKind.Register && instruction.Op1Register is Register.RSP or Register.RBP)
                {
                    // Some clang builds (seen on Linux, Unity 6000.0) pass the address of the slot at [rsp] as "mov rsi, rsp".
                    secondArgumentIsStackAddress = true;
                }
                else if (instruction.FlowControl != FlowControl.Next ||
                         (instruction.Op0Kind == OpKind.Register && instruction.Op0Register.GetFullRegister() == Register.RSI))
                {
                    secondArgumentIsStackAddress = false;
                }
            }
            return found;
        }

        public override IntPtr FindTargetMethod()
        {
            // NOTE: In some cases this pointer will be MetadataCache::GetFieldDefaultValueForField due to Field::GetDefaultFieldValue being
            // inlined but we'll treat it the same even though it doesn't receive the type parameter the RDX register
            // doesn't get cleared so we still get the same parameters
            var classGetDefaultFieldValue = s_Signatures
                .Select(s => MemoryUtils.FindSignatureInModule(InjectorHelpers.Il2CppModule, s))
                .FirstOrDefault(p => p != 0);

            if (classGetDefaultFieldValue == 0)
            {
                Logger.Instance.LogTrace("Couldn't fetch Class::GetDefaultFieldValue with signatures, using method traversal");
                classGetDefaultFieldValue = FindClassGetFieldDefaultValueXref();
            }

            return classGetDefaultFieldValue;
        }
    }
}
