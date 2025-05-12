namespace Il2CppInterop.Runtime.Injection
{
    internal interface IAssemblyListFile
    {
        public bool IsTargetFile(string originalFilePath);
        public void Setup(string originalFilePath);
        public void AddAssembly(string name);
        public string GetOrCreateNewFile();
    }
}
