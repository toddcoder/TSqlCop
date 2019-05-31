using System;
using SqlConformance.Library.SqlContainment;

namespace TSqlCop
{
	public static class ConfigurationProvider
	{
		const string CONFIGURATION_PATH = @"{user}\AppData\Local\TSqlCop\configuration.json";

		public static SqlContainerConfiguration Configuration()
		{
			var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var result = SqlContainerConfiguration.FromFile(CONFIGURATION_PATH.Replace("{user}", user));
			if (result.If(out var configuration, out var exception))
				return configuration;
			else
				throw exception;
		}
	}
}