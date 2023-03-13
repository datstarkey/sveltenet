namespace Svelte.Net.Core.Models
{
	public class SvelteOptions
	{
		public string PagesPath { get; set; } = "Routes";
		public bool EnableSsr { get; set; } = true;
		public bool EnableCsr { get; set; } = true;
	
		public string ClientLocation { get; set; } = "wwwroot/client";
		public string ServerLocation { get; set; } = "wwwroot/server";
	
	
		public string DevServer { get; set; } = "http://localhost:3000";
		public bool IsDev { get; set; } = false;


		public SvelteOptions Clone()
		{
			return new SvelteOptions()
			{
				ClientLocation = ClientLocation,
				ServerLocation = ServerLocation,
				DevServer = DevServer,
				EnableCsr = EnableCsr,
				EnableSsr = EnableSsr,
				IsDev = IsDev,
				PagesPath = PagesPath
			};
		}
	}
}
