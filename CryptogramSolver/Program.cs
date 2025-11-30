using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CryptogramSolver
{
    public static class WordHasher
    {
        /// <summary>
        /// Hashes a word into its similarity equivalent.
        /// MXM -> 010, ASDF -> 0123, AFAFA -> 01010, etc.
        /// </summary>
        public static string HashWord(string word)
        {
            var seen = new Dictionary<char, int>();
            var output = new List<int>();
            var index = 0;

            foreach (var c in word)
            {
                if (!seen.ContainsKey(c))
                {
                    seen[c] = index;
                    index++;
                }
                output.Add(seen[c]);
            }

            return string.Join(string.Empty, output);
        }
    }

    /// <summary>
    /// Manages a corpus of words sorted by frequency descending,
    /// indexed by their hash (similarity pattern).
    /// </summary>
    public class Corpus
    {
        private readonly Dictionary<string, List<string>> _hashDict =
            new Dictionary<string, List<string>>();

        public Corpus(string corpusFilename)
        {
            List<string> wordList = new();

            try
            {
                if (File.Exists(corpusFilename))
                {
                    wordList = File.ReadAllLines(corpusFilename)
                                   .Where(line => !string.IsNullOrWhiteSpace(line))
                                   .ToList();
                }
                else
                {
                    Console.WriteLine($"Corpus file not found: {corpusFilename}");
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
            }

            foreach (var word in wordList)
            {
                var wordHash = WordHasher.HashWord(word);
                if (!_hashDict.TryGetValue(wordHash, out var list))
                {
                    list = new List<string>();
                    _hashDict[wordHash] = list;
                }
                list.Add(word);
            }
        }

        /// <summary>
        /// Finds words in the corpus that could match the given word in ciphertext.
        /// Uppercase letters = ciphertext; lowercase letters = plaintext.
        /// </summary>
        public List<string> FindCandidates(string inputWord)
        {
            var inputWordHash = WordHasher.HashWord(inputWord);
            _hashDict.TryGetValue(inputWordHash, out var matchesHash);
            matchesHash ??= new List<string>();

            var candidates = new List<string>();

            foreach (var word in matchesHash)
            {
                bool invalid = false;

                for (int i = 0; i < word.Length; i++)
                {
                    char inChar = inputWord[i];
                    char corpusChar = word[i];

                    // If either is lowercase or apostrophe, they must match exactly.
                    if ((char.IsLower(inChar) || inChar == '\'' || corpusChar == '\'')
                        && inChar != corpusChar)
                    {
                        invalid = true;
                        break;
                    }
                }

                if (!invalid)
                {
                    candidates.Add(word);
                }
            }

            return candidates;
        }
    }

    /// <summary>
    /// Solves substitution ciphers by recursive search over a corpus.
    /// </summary>
    public class SubSolver
    {
        private readonly Corpus _corpus;
        private Dictionary<char, char> _translation = new();
        public string Ciphertext { get; }
        public bool Verbose { get; }

        public SubSolver(string ciphertext, string corpusFilename, bool verbose = false)
        {
            _corpus = new Corpus(corpusFilename);
            Ciphertext = ciphertext.ToUpperInvariant();
            Verbose = verbose;
        }

        public void Solve()
        {
            // Strip non-word, non-space chars and split to words.
            var cleaned = Regex.Replace(Ciphertext, @"[^\w ]+", string.Empty);
            var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

            // Sort by descending length.
            words.Sort((a, b) => b.Length.CompareTo(a.Length));

            // Try increasing thresholds for unknown words.
            int maxIterations = Math.Max(3, words.Count / 10);
            for (int maxUnknownWordCount = 0; maxUnknownWordCount < maxIterations; maxUnknownWordCount++)
            {
                var solution = RecursiveSolve(words, new Dictionary<char, char>(), 0, maxUnknownWordCount);
                if (solution != null)
                {
                    _translation = solution;
                    break;
                }
            }
        }

        private Dictionary<char, char>? RecursiveSolve(
            IList<string> remainingWords,
            Dictionary<char, char> currentTranslation,
            int unknownWordCount,
            int maxUnknownWordCount)
        {
            var trans = MakeTransFromDict(currentTranslation);

            if (Verbose)
            {
                Console.WriteLine(ApplyTranslation(Ciphertext, trans));
            }

            if (remainingWords.Count == 0)
            {
                return currentTranslation;
            }

            if (unknownWordCount > maxUnknownWordCount)
            {
                return null;
            }

            var cipherWord = remainingWords[0];
            var translatedCipherWord = ApplyTranslation(cipherWord, trans);

            var candidates = _corpus.FindCandidates(translatedCipherWord);

            foreach (var candidate in candidates)
            {
                var newTrans = new Dictionary<char, char>(currentTranslation);
                var translatedPlaintextChars = new HashSet<char>(currentTranslation.Values);
                bool badTranslation = false;

                for (int i = 0; i < candidate.Length; i++)
                {
                    char cipherChar = cipherWord[i];
                    char plainChar = candidate[i];

                    // Bad if we try to map an unseen cipher char to a plaintext char
                    // that is already mapped from a different cipher char.
                    if (!currentTranslation.ContainsKey(cipherChar)
                        && translatedPlaintextChars.Contains(plainChar))
                    {
                        badTranslation = true;
                        break;
                    }

                    newTrans[cipherChar] = plainChar;
                }

                if (badTranslation)
                {
                    continue;
                }

                var subRemaining = remainingWords.Skip(1).ToList();
                var result = RecursiveSolve(subRemaining, newTrans, unknownWordCount, maxUnknownWordCount);
                if (result != null)
                {
                    return result;
                }
            }

            // Try skipping this word (could be a proper noun not in corpus).
            {
                var subRemaining = remainingWords.Skip(1).ToList();
                var skipWordSolution = RecursiveSolve(
                    subRemaining,
                    currentTranslation,
                    unknownWordCount + 1,
                    maxUnknownWordCount);

                if (skipWordSolution != null)
                {
                    return skipWordSolution;
                }
            }

            return null;
        }

        private static Dictionary<char, char> MakeTransFromDict(Dictionary<char, char> translations)
        {
            // In Python this returns a mapping usable with string.translate().
            // Here we just return a copy (dictionary itself is the mapping).
            return new Dictionary<char, char>(translations);
        }

        private static string ApplyTranslation(string input, Dictionary<char, char> trans)
        {
            var chars = input.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (trans.TryGetValue(chars[i], out var mapped))
                {
                    chars[i] = mapped;
                }
            }
            return new string(chars);
        }

        public void PrintReport()
        {
            if (_translation == null || _translation.Count == 0)
            {
                Console.WriteLine("Failed to translate ciphertext.");
                return;
            }

            var trans = MakeTransFromDict(_translation);
            var plaintext = ApplyTranslation(Ciphertext, trans);

            Console.WriteLine("Ciphertext:");
            Console.WriteLine(Ciphertext);
            Console.WriteLine();

            Console.WriteLine("Plaintext:");
            Console.WriteLine(plaintext);
            Console.WriteLine();

            Console.WriteLine("Substitutions:");
            var items = _translation
                .Select(kv => $"{kv.Key} -> {kv.Value}")
                .OrderBy(s => s)
                .ToList();

            for (int i = 0; i < items.Count; i++)
            {
                Console.Write(items[i] + " ");
                if (i % 5 == 4)
                {
                    Console.WriteLine();
                }
            }

            Console.WriteLine();
        }
    }

    internal static class Program
    {
        // Rough equivalent of Python's argparse usage:
        // sub_solver.py input_text -c corpus.txt -v
        private const string Version = "0.1.0";

        public static void Main(string[] args)
        {
            Console.WriteLine($"SubSolver v{Version}");
            Console.WriteLine();

            if (args.Length < 1)
            {
                Console.WriteLine("Usage: CryptogramSolver <input_file> [-c corpus.txt] [-v]");
                return;
            }

            string inputFile = args[0];
            var corpusPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptogramSolver", "corpus.txt");
            bool verbose = false;

            // Simple manual arg parsing for -c and -v
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "-c" && i + 1 < args.Length)
                {
                    corpusPath = args[i + 1];
                    i++;
                }
                else if (args[i] == "-v")
                {
                    verbose = true;
                }
            }

            string ciphertext;
            try
            {
                ciphertext = File.ReadAllText(inputFile).Trim();
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            var solver = new SubSolver(ciphertext, corpusPath, verbose);
            solver.Solve();
            solver.PrintReport();
        }
    }
}
