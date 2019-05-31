using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace TSqlCop
{
	[Export(typeof(SqlNonConformanceFormatDefinition)), Name(Constants.TAG_DEFINITION), UserVisible(true)]
	public class SqlNonConformanceFormatDefinition : MarkerFormatDefinition
	{
		public SqlNonConformanceFormatDefinition()
		{
			var orange = Brushes.Orange.Clone();
			orange.Opacity = 0.25;
			Fill = orange;
			Border = new Pen(Brushes.Gray, 1.0);
			DisplayName = "Highlight Word";
			ZOrder = 5;
		}
	}
}