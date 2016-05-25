namespace SeedRetriever
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Security.Cryptography.X509Certificates;
	using System.Text;
	using System.Xml;
	using System.Xml.Linq;
	using System.Xml.XPath;

	using Shameless.Utils;

	public static class SeedRetriever
	{
		private static readonly X509Certificate2 certificate = new X509Certificate2(File.ReadAllBytes("ctr-common.pfx"));
		public static void Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;

			// var allTitles = File.ReadAllLines("titlesBig.txt");
			var foundTitles = File.ReadAllLines("validTitles.txt");
			var titlesChecked = Directory.GetFiles("metadata\\").Select(a => a.Substring(9, a.Length - 9).Split(' ')[0]);

			var titles = foundTitles.Except(titlesChecked).ToArray();
			
			const string LastIndexPath = "lastIndex.txt";

			var lastIndex = 0; // int.Parse(File.ReadAllText(LastIndexPath));

			for (var i = lastIndex; i < titles.Length; i++)
			{
				var titleId = titles[i];

				CDNUtils.RetrieveTitleData(titleId, certificate);

				File.WriteAllText(LastIndexPath, i.ToString());
			}
		}

		private static void DownloadIcons()
		{
			var titlesPaths = Directory.GetFiles("metadata");
			var iconsPaths = Directory.GetFiles("icons");

			foreach (var titlesPath in titlesPaths)
			{
				var doc = XDocument.Parse(File.ReadAllText(titlesPath));
				var url = doc.Document.Descendants("title").First().Descendants().FirstOrDefault(a => a.Name == "icon_url");

				if (url == null)
				{
					continue;
				}

				var filename = new FileInfo(titlesPath).Name;
				var titleId = filename.Split(' ')[0];

				if (iconsPaths.Any(a => a.Contains(titleId)))
				{
					continue;
				}

				var name = filename.Split(new []{" - "}, StringSplitOptions.None).Last().Trim().Replace(".xml", ".jpg");

				using (var client = new CertificateWebClient(certificate))
				{
					File.WriteAllBytes($"icons\\{titleId} - {name}", client.DownloadData(url.Value));
					Console.WriteLine($"Downloaded icon for {name}");
				}
			}
		}

		private static void BruteforceTitles(IEnumerable<string> titlesChecked)
		{
			var titles = Enumerable.Range(0x0000, 0x2000).Select(a => $"000400F7{a:X6}00").ToArray();

			var titlesToCheck = titles.Except(titlesChecked).ToArray();

			foreach (var titleId in titlesToCheck)
			{
				using (var client = new CertificateWebClient(certificate))
				{
					var internalId = CDNUtils.RetrieveInternalInfo(titleId, client)[0];

					if (internalId != string.Empty)
					{
						Console.WriteLine($"\r{titleId}: {internalId}");
						File.AppendAllText("validTitles.txt", titleId + Environment.NewLine);
					}
					else
					{
						Console.Write($"\r{titleId}: ");
					}
				}
			}
		}

		private static void ExportTitles()
		{
			var doc = XDocument.Parse(File.ReadAllText("community.xml"));
			var titles = doc.Descendants().Descendants("Ticket").Select(a => a.Descendants().Last().Value).ToArray();

			File.WriteAllLines("titlesBig.txt", titles);
		}

		private static void OutputSeeds()
		{
			var seeds = File.ReadAllLines("seeds.txt")
				.Select(a => a.Split(' '))
				.ToDictionary(a => a[0], k => BitConversion.HexToBytes(k[1]));

			foreach (var pair in seeds)
			{
				const string seedPath = @"seeds\";
				File.WriteAllBytes(seedPath + pair.Key + ".dat", pair.Value);
			}
		}
	}
}