using System;
using System.Collections.Generic;

namespace MediaBrowser.Tests.ConsistencyTests.TextIndexing
{
    public class WordIndex : Dictionary<string, WordOccurrences>
    {
        public WordIndex() : base(StringComparer.InvariantCultureIgnoreCase)
        {
        }

        public void AddWordOccurrence(string word, string fileName, string fullPath, int lineNumber, int wordIndex)
        {
            WordOccurrences current;
            if (!TryGetValue(word, out current))
            {
                current = new WordOccurrences();
                this[word] = current;
            }

            current.AddOccurrence(fileName, fullPath, lineNumber, wordIndex);
        }

        public WordOccurrences Find(string word)
        {
           WordOccurrences found;
           if (TryGetValue(word, out found))
           {
               return found;
           }

           return null;
        }

    }
}
