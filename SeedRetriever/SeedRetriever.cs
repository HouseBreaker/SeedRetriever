namespace SeedRetriever
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Security.Cryptography.X509Certificates;
	using System.Text;
	using System.Threading.Tasks;
	using System.Xml.Linq;

	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;

	using Shameless.Utils;

	using Formatting = Newtonsoft.Json.Formatting;

	public static class SeedRetriever
	{
		private static readonly X509Certificate2 Certificate = new X509Certificate2(File.ReadAllBytes("ctr-common.pfx"));

		public static void Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;
			GetDemos();
		}

		private static void GetDemos()
		{
			var titleFiles = Directory.GetFiles("metadata\\");

			var titles = titleFiles.Select(titleFile => JObject.Parse(File.ReadAllText(titleFile))["title"]);

			var demos = titles.Where(a => a["demo_titles"] != null);

			foreach (var demoTitles in demos)
			{
				foreach (var token in demoTitles["demo_titles"])
				{
					var title = token["demo_title"];
					// Console.WriteLine(title.Replace("\n", " "));
				}
			}
		}

		private static void GetSerials()
		{
			var titleFiles = Directory.GetFiles("metadata\\");

			var titles = titleFiles.Select(titleFile => JObject.Parse(File.ReadAllText(titleFile))["title"]);
			var serials = titles.Select(json => json["product_code"].ToString()).ToArray();

			var digitalSerials = serials.Where(a => a[4] == 'N').ToArray();
			var physicalSerials = serials.Where(a => a[4] == 'P').ToArray();
		}

		private static void DownloadAllTitleMetadata()
		{
			var titles = JObject.Parse(File.ReadAllText("countries.json"));
			var processed = Directory.GetFiles("metadata\\").Select(a => a.Substring(9, 16)).ToArray();

			var props = titles.Properties();
			var propsToCheck = props.Where(prop => !processed.Contains(prop.Name)).ToArray();

			foreach (var title in propsToCheck)
			{
				var titleId = title.Name;
				var internalUid = title.Value["internaluid"].ToString();
				var countries = title.Value["countries"].ToObject<string[]>();

				var data = new TitleData(titleId);
				foreach (var country in countries)
				{
					data = CDNUtils.RetrieveTitleData(new[] { internalUid, "T" }, titleId, country, Certificate);

					if (data.HasMetadata())
					{
						break;
					}
				}

				var filename = CDNUtils.SanitizeFilename($"{titleId} {internalUid} - {data.Name}.json");
				var serializedJson = JsonConvert.SerializeObject(data.MetadataJson, Formatting.Indented);

				File.WriteAllText("metadata\\" + filename, serializedJson);
				Console.WriteLine($"{titleId}: " + data.Name);
			}
		}

		private static void GetTitlesCountries()
		{
			var titles = JObject.Parse(File.ReadAllText("IDPairs_sorted.json")).Children().Cast<JProperty>().ToArray();
			var countries = new Dictionary<string, string>();

			foreach (var countryFile in Directory.GetFiles("titles\\"))
			{
				var key = countryFile.Substring(7, 2);
				var contents = File.ReadAllText(countryFile);

				countries[key] = contents;
			}

			var countriesDictionary = new SortedDictionary<string, object>();

			Parallel.ForEach(
				titles,
				(title) =>
					{
						var internalId = title.Value.Value<long>();
						var titleId = title.Name;

						var validCountries = countries.Where(a => a.Value.Contains(internalId.ToString())).Select(a => a.Key).ToArray();

						var data = new { internaluid = internalId, countries = validCountries };

						countriesDictionary[titleId] = data;

						Console.WriteLine($"{titleId}: {string.Join(", ", validCountries)}");
					});

			var json = JsonConvert.SerializeObject(countriesDictionary, Formatting.Indented);
			File.WriteAllText("countries.json", json);
		}

		private static void ListTitlesForCountry(string country, X509Certificate2 certificate)
		{
			var titles =
				JObject.Parse(File.ReadAllText($"titles\\{country}.json"))["contents"]["content"].Where(a => a["title"] != null)
					.ToArray();

			foreach (var id in titles)
			{
				string titleId;
				using (var client = new CertificateWebClient(certificate))
				{
					client.Headers.Add("Accept", "application/json");

					var response = client.DownloadString($"https://ninja.ctr.shop.nintendo.net/ninja/ws/titles/id_pair?ns_uid[]={id}");
					var json = JObject.Parse(response);
					titleId = json["title_id_pairs"]["title_id_pair"].First["title_id"].ToString();
				}

				Console.WriteLine($"{id} {titleId}");
				File.AppendAllText("ID_Pairs.json", $"\t{id}: \"{titleId}\",\r\n");
			}
		}

		private static void GetUniqueTitles()
		{
			var allTitles = new HashSet<string>();

			foreach (var file in Directory.GetFiles("titles\\"))
			{
				var json = JObject.Parse(File.ReadAllText(file))["contents"]["content"]?.Where(a => a["title"] != null);

				if (json != null)
				{
					var internalIds = json.Select(a => a["title"]["id"].ToString());

					foreach (var internalId in internalIds)
					{
						allTitles.Add(internalId);
					}
				}

				Console.WriteLine(file.Substring(7, 2));
			}

			foreach (var title in allTitles)
			{
				File.AppendAllText("uniqueIDs.txt", title + Environment.NewLine);
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

				var name = filename.Split(new[] { " - " }, StringSplitOptions.None).Last().Trim().Replace(".xml", ".jpg");

				using (var client = new CertificateWebClient(Certificate))
				{
					File.WriteAllBytes($"icons\\{titleId} - {name}", client.DownloadData(url.Value));
					Console.WriteLine($"Downloaded icon for {name}");
				}
			}
		}

		private static void DownloadPackageImages()
		{
			var titlesPaths = Directory.GetFiles("metadata");
			var iconsPaths = Directory.GetFiles("packages");

			foreach (var titlesPath in titlesPaths)
			{
				var doc = XDocument.Parse(File.ReadAllText(titlesPath));
				var url = doc.Document.Descendants("title").First().Descendants().FirstOrDefault(a => a.Name == "package_url");

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

				var name = filename.Split(new[] { " - " }, StringSplitOptions.None).Last().Trim().Replace(".xml", ".jpg");

				using (var client = new CertificateWebClient(Certificate))
				{
					File.WriteAllBytes($"packages\\{titleId} - {name}", client.DownloadData(url.Value));
					Console.WriteLine($"Downloaded package image for {name}");
				}
			}
		}

		private static void BruteforceTitles(IEnumerable<string> titlesChecked)
		{
			var titles = Enumerable.Range(0x0000, 0x2000).Select(a => $"000400F7{a:X6}00").ToArray();

			var titlesToCheck = titles.Except(titlesChecked).ToArray();

			foreach (var titleId in titlesToCheck)
			{
				var internalId = CDNUtils.RetrieveInternalInfo(titleId, Certificate)[0];

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