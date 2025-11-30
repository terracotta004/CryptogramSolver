using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EnglishCorpusGenerator
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            // Equivalent of: files = glob.glob('pages/*')
            var pagesFolder = Path.Combine(AppContext.BaseDirectory, "pages");
            if (args.Length > 0)
            {
                pagesFolder = Path.Combine(AppContext.BaseDirectory, args[0]);
            }
            
            if (!Directory.Exists(pagesFolder))
            {
                Console.WriteLine($"Directory not found: {pagesFolder}");
                return;
            }

            var files = Directory.GetFiles(pagesFolder);

            var words = new List<(int Rank, string Word)>();

            // Python regex: r'<tr>\n<td>([0-9]+)</td>\n<td><a[^>]*>([^<]*)</a></td>'
            var regex = new Regex(
                @"<tr>\s*<td>([0-9]+)</td>\s*<td><a[^>]*>([^<]*)</a></td>",
                RegexOptions.Compiled);

            foreach (var filename in files)
            {
                string page;
                try
                {
                    page = File.ReadAllText(filename);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Error reading file {filename}: {ex.Message}");
                    continue;
                }

                var matches = regex.Matches(page);
                foreach (Match match in matches)
                {
                    if (!match.Success) continue;

                    if (int.TryParse(match.Groups[1].Value, out int rank))
                    {
                        string word = match.Groups[2].Value;
                        words.Add((rank, word));
                    }
                }
            }

            // Sort by numeric rank, ascending.
            words.Sort((a, b) => a.Rank.CompareTo(b.Rank));

            var corpusPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CryptogramSolver", "corpus.txt");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(corpusPath)!);
                File.WriteAllLines(corpusPath, words.Select(w => w.Word));
                Console.WriteLine($"Wrote {words.Count} words to {corpusPath}.");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error writing corpus: {ex.Message}");
            }
        }
    }
}
