namespace SeedRetriever
{
	using Newtonsoft.Json.Linq;

	public class TitleData
	{
		public TitleData(string titleId, string internalId, string type, JObject metadataJson, string name)
		{
			this.TitleId = titleId;
			this.InternalId = internalId;
			this.Type = type;
			this.MetadataJson = metadataJson;
			this.Name = name;
		}

		public TitleData(string titleId, string internalId, string type)
		{
			this.TitleId = titleId;
			this.InternalId = internalId;
			this.Type = type;
		}

		public TitleData(string titleId)
		{
			this.TitleId = titleId;
		}

		public string TitleId { get; set; }

		public string InternalId { get; set; }

		public string Type { get; set; }

		public JObject MetadataJson { get; set; }

		public string Name { get; set; }

		public bool HasInternalId()
		{
			return this.InternalId != null;
		}

		public bool HasMetadata()
		{
			return this.MetadataJson != null;
		}
	}
}