using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace TSqlCop
{
	[Export, Export(typeof(IViewTaggerProvider)), ContentType("text"), TagType(typeof(IErrorTag)),
	 TextViewRole(PredefinedTextViewRoles.Document), TextViewRole(PredefinedTextViewRoles.Analyzable)]
	internal sealed class SqlNonConformanceTaggerProvider : IViewTaggerProvider
	{
		[ImportingConstructor]
		public SqlNonConformanceTaggerProvider() { }

		[Import]
		internal ITextSearchService TextSearchService { get; set; }

		[Import]
		internal ITextStructureNavigatorSelectorService TextStructureNavigatorSelectorService { get; set; }

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
		{
			if (textView.TextBuffer != buffer)
				return null;
			else
			{
				var configuration = ConfigurationProvider.Configuration();
				return (ITagger<T>)new SqlNonConformanceTagger(buffer, configuration);
			}
		}
	}
}