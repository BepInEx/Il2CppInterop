# Exceptions

Il2Cpp exceptions each have a cooresponding system exception generated, making up a full hierarchy and enabling try catch support in unstripped code.

## Runtime exceptions

Unstripped code currently allows exceptions (such as `NullReferenceException`) to be thrown by the .NET runtime. Ideally, all such exceptions be handled.
