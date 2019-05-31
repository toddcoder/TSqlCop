using Microsoft.VisualStudio.Text.Tagging;
using SqlConformance.Library.SqlConformance;

namespace TSqlCop
{
	public class SqlNonConformanceTag : ErrorTag
	{
		public SqlNonConformanceTag(NonConformance conformance) : base(Constants.TAG_DEFINITION, conformance.Message) { }
	}
}