using System;
using System.Linq;

namespace Shameless.Utils
{
	using System.IO;
	using System.Net;
	using System.Security.Cryptography.X509Certificates;
	using System.Text;
	using System.Xml.Linq;

	using SeedRetriever;

	public static class CDNUtils
	{
		public static long GetTitleSize(string titleId)
		{
			// translated from FunKeyCIA
			var cdnUrl = "http://ccs.cdn.c.shop.nintendowifi.net/ccs/download/" + titleId.ToUpper();

			byte[] tmd;

			using (var client = new WebClient())
			{
				tmd = client.DownloadData(cdnUrl + "/tmd");
			}

			const int TikOffset = 0x140;

			var contentCount = Convert.ToInt32(BitConversion.BytesToHex(tmd.Skip(TikOffset + 0x9E).Take(2)), 16);

			long size = 0;

			for (int i = 0; i < contentCount; i++)
			{
				var contentOffset = 0xB04 + 0x30 * i;
				var contentId = BitConversion.BytesToHex(tmd.Skip(contentOffset).Take(4));

				try
				{
					var req = WebRequest.Create(cdnUrl + "/" + contentId);

					using (var resp = req.GetResponse())
					{
						long currentSize;
						if (long.TryParse(resp.Headers.Get("Content-Length"), out currentSize))
						{
							size += currentSize;
						}
					}
				}
				catch (WebException)
				{
				}
			}

			return size;
		}

		public static void RetrieveSeed(string titleId)
		{
			ServicePointManager.ServerCertificateValidationCallback = (sender, x509Certificate, chain, errors) => true;
			const string country = "US";

			using (var client = new WebClient())
			{
				try
				{
					const string CdnUrl = "https://kagiya-ctr.cdn.nintendo.net/title/0x{0}/ext_key?country={1}";
					var seedAsBytes = client.DownloadData(string.Format(CdnUrl, titleId, country));

					var seed = BitConversion.BytesToHex(seedAsBytes);
					var titleSeedPair = $"{titleId} {seed}{Environment.NewLine}";

					Console.Write(titleSeedPair);

					var fileContents = File.ReadAllText("seeds.txt");
					if (!fileContents.Contains(titleSeedPair))
					{
						File.AppendAllText("seeds.txt", titleSeedPair);
					}
				}
				catch (WebException ex)
				{
					var response = (HttpWebResponse)ex.Response;
					var statusCode = (int)response.StatusCode;

					Console.WriteLine($"{titleId} {statusCode}");
				}
			}
		}

		public static void RetrieveTitleData(string titleId, X509Certificate2 certificate)
		{
			using (var client = new CertificateWebClient(certificate))
			{
				client.Encoding = Encoding.UTF8;
				try
				{
					Console.Write(titleId + ": ");

					var internalInfo = RetrieveInternalInfo(titleId, client);

					if (internalInfo[0] == string.Empty)
					{
						Console.WriteLine("No info");
						return;
					}

					var internalId = internalInfo[0];
					var type = internalInfo[1];

					Console.Write($"{internalId} ({type}) - ");

					var countries = new[] { "US", "JP", "HK", "TW", "KR", "DE", "GB", "FR", "ES", "NL", "IT" };

					var metadataResponse = string.Empty;
					foreach (var country in countries)
					{
						try
						{
							const string metadataUrl = "https://samurai.ctr.shop.nintendo.net/samurai/ws/";
							metadataResponse = client.DownloadString(metadataUrl + country + "/title/" + internalId);
							break;
						}
						catch (WebException ex)
						{
							// continue
						}
					}

					if (metadataResponse == string.Empty)
					{
						Console.WriteLine("No info");
						return;

						// 	for (int i = 'A'; i <= 'Z'; i++)
						// 	{
						// 		for (int j = 'A'; j <= 'Z'; j++)
						// 		{
						// 			var country = $"{(char)i}{(char)j}";
						// 			try
						// 			{
						// 				const string metadataUrl = "https://samurai.ctr.shop.nintendo.net/samurai/ws/";
						// 				metadataResponse = client.DownloadString(metadataUrl + country + "/title/" + internalId);

						// 				Console.CursorLeft = Console.CursorLeft == 37 ? Console.CursorLeft + 1 : Console.CursorLeft - 2;
						// 				Console.Write(country);
						// 				break;
						// 			}
						// 			catch (WebException ex)
						// 			{
						// 				Console.CursorLeft = Console.CursorLeft == 37 ? Console.CursorLeft + 1 : Console.CursorLeft - 2;
						// 				Console.Write(country);
						// 			}
						// 		}
						// 	}
					}

					var metadataXml = XDocument.Parse(metadataResponse);

					var name = metadataXml.Descendants().First(a => a.Name == "formal_name").Value.Replace("\n", " ");
					Console.WriteLine(name);

					var filename = SanitizeFilename($"{titleId} ({internalId}) - {name}.xml");
					File.WriteAllText("metadata\\" + filename, metadataXml.ToString());
				}
				catch (WebException ex)
				{
					var response = (HttpWebResponse)ex.Response;
					var statusCode = (int)response.StatusCode;

					Console.WriteLine($"{titleId} {statusCode}");
				}
			}
		}

		public static string[] RetrieveInternalInfo(string titleId, CertificateWebClient client)
		{
			// from PlaiCDN
			var internalInfo = new[] { string.Empty, string.Empty };

			var url = $"https://ninja.ctr.shop.nintendo.net/ninja/ws/titles/id_pair?title_id[]={titleId}";
			var response = client.DownloadString(url);

			var xml = XDocument.Parse(response);

			var xElements = xml.Descendants("title_id_pairs");
			if (!xElements.First().HasElements)
			{
				// Console.WriteLine($"{titleId}: No info");
				return internalInfo;
			}

			var nodes = xml.Descendants("title_id_pair").First().Descendants().DescendantsAndSelf();

			if (!nodes.Any())
			{
				// Console.WriteLine($"{titleId}: No info");
				return internalInfo;
			}

			var internalId = nodes.First(a => a.Name == "ns_uid").Value;
			var type = nodes.First(a => a.Name == "type").Value;

			internalInfo[0] = internalId;
			internalInfo[1] = type;

			return internalInfo;
		}

		private static string SanitizeFilename(string input)
		{
			var forbiddenChars = Path.GetInvalidFileNameChars().ToList();
			forbiddenChars.AddRange(new[] { '\n', '™', '©', '®' });

			var sanitized = new StringBuilder();

			foreach (var letter in input)
			{
				sanitized.Append(forbiddenChars.Contains(letter) ? string.Empty : letter.ToString());
			}

			return sanitized.ToString();
		}
	}
}