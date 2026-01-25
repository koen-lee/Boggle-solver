using System.Diagnostics;

namespace BoggleSolverConsole
{
    class ProgramSerialize
    {
        static void Main(string[] args)
        {
            var filename = "woorden.txt";
            Console.WriteLine("Reading {0}", filename);
            var words = File.ReadAllLines(filename);
            Console.WriteLine("Loading dictionary {0}", filename);
            CharDictionaryEntry dictionary;
            using (var stopwatch = new ConsoleStopwatch("Loading dictionary"))
                dictionary = BoggleUtilities.LoadWords(words/*, UniqueCharsIn(field)*/);
            FileInfo wordFile = new FileInfo(filename);
            Console.WriteLine($"Word file: {wordFile.FullName}, size: {wordFile.Length:N0} bytes");
            var serializedFile = Path.ChangeExtension(wordFile.FullName, ".bin");
            //Write serialized dictionary
            using (var stopwatch = new ConsoleStopwatch("Serializing dictionary"))
            using (var fs = File.Create(serializedFile))
            using (var writer = new BinaryWriter(fs))
                dictionary.WriteTo(writer);
            FileInfo serializedFileInfo = new FileInfo(serializedFile);
            Console.WriteLine($"Serialized dictionary to: {serializedFileInfo.FullName}, size: {serializedFileInfo.Length:N0} bytes");
          
            //Read serialized dictionary
            CharDictionaryEntry deserializedDictionary;
            using (var stopwatch = new ConsoleStopwatch("Deserializing dictionary"))
            using (var fs = File.OpenRead(serializedFile))
            using (var reader = new BinaryReader(fs))
                deserializedDictionary = CharDictionaryEntry.ReadFrom(reader);
            Console.WriteLine("Deserialized dictionary");

            // Verify dictionaries are the same
            using (var stopwatch = new ConsoleStopwatch("Verifying dictionaries"))
            {
                var wordsInDeserialized = EnumerateWords(deserializedDictionary);
                var wordsInDic = EnumerateWords(dictionary);
                foreach (var pair in wordsInDic.Zip(wordsInDeserialized, (w1, w2) => (w1, w2)))
                {
                    if (!String.Equals(pair.w1, pair.w2, StringComparison.InvariantCultureIgnoreCase))
                        throw new InvalidOperationException($"Dictionaries do not match! Word '{pair.w1}' != '{pair.w2}'");
                }
            }
        }

        private static IEnumerable<string> EnumerateWords(CharDictionaryEntry deserializedDictionary)
        {
            if (deserializedDictionary.IsWord)
                yield return deserializedDictionary.Word;
            foreach (var next in deserializedDictionary.NextEntries)
                foreach (var word in EnumerateWords(next))
                    yield return word;
        }
    }

    class ConsoleStopwatch : IDisposable
    {
        private Stopwatch stopwatch;
        private string message;

        public ConsoleStopwatch(string message)
        {
            this.message = message;
            stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            stopwatch.Stop();
            Console.WriteLine("{0} took {1} ms", message, stopwatch.ElapsedMilliseconds);
        }
    }
}
