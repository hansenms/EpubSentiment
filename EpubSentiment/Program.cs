using System;
using System.Text;
using HtmlAgilityPack;
using VersOne.Epub;
using Microsoft.ProjectOxford.Text.Core.Exceptions;
using Microsoft.ProjectOxford.Text.Sentiment;

namespace EpubSentiment
{
    class Program
    {

        static void AppendChapter(ref SentimentRequest request, EpubChapter chapter)
        {
            HtmlDocument htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(chapter.HtmlContent);

            StringBuilder sb = new StringBuilder();

            foreach (HtmlNode node in htmlDocument.DocumentNode.SelectNodes("//text()"))
            {
                sb.AppendLine(node.InnerText.Trim());
            }

            string chapterText = sb.ToString();

            int maxCharacters = 3 * 1024; //Max characters that we will send to sentiment API
            int chunks = (int)Math.Ceiling((double)chapterText.Length / (double)maxCharacters);
            int charsPerChunk = (int)Math.Ceiling((double)chapterText.Length / (double)chunks);

            int offset = 0;

            for (int i = 0; i < chunks; ++i)
            {
                if (offset + charsPerChunk > chapterText.Length)
                {
                    charsPerChunk = chapterText.Length - offset;
                }

                var testText = chapterText.Substring(offset, charsPerChunk);
                string chunkID = "CHUNKDOCUMENT" + i;
                var doc = new SentimentDocument() { Id = chunkID, Text = testText, Language = "en" };

                request.Documents.Add(doc);

                offset += charsPerChunk;
            }


        }

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: ");
                Console.WriteLine("  " + System.AppDomain.CurrentDomain.FriendlyName + " <FILENAME> <APIKEY>");
                Environment.Exit(1);
            }
            string bookfile = args[0];
            string apiKey = args[1];

            Console.WriteLine("Analyzing book: " + bookfile);
            EpubBook epubBook = EpubReader.ReadBook(bookfile);

            string title = epubBook.Title;
            string author = epubBook.Author;

            Console.WriteLine("Book title: " + title);
            Console.WriteLine();

            double bookScore = 0.0;
            int numChapters = 0;
            foreach (EpubChapter chapter in epubBook.Chapters)
            {

                var request = new SentimentRequest();

                string chapterTitle = chapter.Title;

                AppendChapter(ref request, chapter);

                foreach (EpubChapter subChapter in chapter.SubChapters)
                {
                    AppendChapter(ref request, subChapter);
                }
           
                var client = new SentimentClient(apiKey);
                var response = client.GetSentiment(request);

                foreach (Microsoft.ProjectOxford.Text.Core.DocumentError e in response.Errors)
                {
                    Console.WriteLine("Errors: " + e.Message);
                }

                double score = 0.0;
                int numScores = 0;

                foreach (SentimentDocumentResult r in  response.Documents)
                {
                    score += r.Score;
                    numScores++;
                }

                score /= numScores;

                Console.WriteLine(numChapters + ": " + chapterTitle + ", score: " + score);

                bookScore += score;
                numChapters++;
            }

            bookScore /= numChapters;

            Console.WriteLine();
            Console.WriteLine("Average book sentiment: " + bookScore);
        }
    }
}
