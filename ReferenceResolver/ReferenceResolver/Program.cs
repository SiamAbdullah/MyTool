namespace ReferenceResolver
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection.Metadata;
    using System.Reflection.Metadata.Ecma335;
    using System.Reflection.PortableExecutable;

    public class Program
    {
        private const string OutPutFileExtension = "info.txt";
        private static Dictionary<string, Func<MetadataReader, IEnumerable<string>>> MethodMapping = new Dictionary<string, Func<MetadataReader, IEnumerable<string>>>()
        {
            { "-references", GetReferencesFromDll}
        };

        public static void Main(string[] args)
        {
            if(args.Length < 2)
            {
                Console.WriteLine("Usage: ReferenceResolver.exe DllName -option");
                Console.WriteLine("Option: -references");
            }

            var dllName = args[0];
            var outputFileName = Path.GetFileName(dllName) + OutPutFileExtension;

            using (var fs = new FileStream(dllName, FileMode.Open, FileAccess.Read))
            {
                using (var re = new PEReader(fs))
                {
                    var writer = new StreamWriter(outputFileName);

                    if (re.HasMetadata)
                    {
                        var reader = re.GetMetadataReader();

                        for (var i = 1; i < args.Length; i++)
                        {
                            Func<MetadataReader, IEnumerable<string>> method;
                            if (MethodMapping.TryGetValue(args[1], out method))
                            {
                                var references = method(reader);
                                WriteInFile(writer, "References", references);
                            }
                        }
                    }

                    writer.Flush();
                    writer.Close();

                    Console.WriteLine("Output FileName : {0}", outputFileName);
                }
            }
        }

        private static void WriteInFile(StreamWriter wr, string section, IEnumerable<string> values)
        {
            wr.WriteLine(section);
            foreach (var value in values)
            {
                wr.WriteLine(value);
            }

            wr.WriteLine("\n---------------------------------------------------------------------------------------------");
        }

        private static IEnumerable<string> GetReferencesFromDll(MetadataReader reader)
        {
            var typeReferenceHandles = reader.TypeReferences;
            var dictionary = new Dictionary<int, string>(100);

            foreach (var typeReferenceHandle in typeReferenceHandles)
            {
                var typeReference = reader.GetTypeReference(typeReferenceHandle);
                var resolutionScope = typeReference.ResolutionScope;
                if (resolutionScope.Kind == HandleKind.AssemblyReference)
                {
                    var rowNumber = reader.GetToken(resolutionScope);

                    if (!dictionary.TryGetValue(rowNumber, out var assemblyName))
                    {
                        var assemblyReference = reader.GetAssemblyReference(MetadataTokens.AssemblyReferenceHandle(rowNumber));
                        assemblyName = reader.GetString(assemblyReference.Name);
                        dictionary.Add(rowNumber, assemblyName);
                    }
                }
            }

            return dictionary.Values;
        }
    }
}
