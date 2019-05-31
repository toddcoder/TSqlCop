using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using SqlConformance.Library.SqlContainment;

namespace TSqlCop
{
	public class SqlNonConformanceTagger : ITagger<IErrorTag>
	{
		ITextBuffer textBuffer;
		SqlContainerConfiguration configuration;

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

      public SqlNonConformanceTagger(ITextBuffer textBuffer, SqlContainerConfiguration configuration)
		{
			this.textBuffer = textBuffer;
			this.configuration = configuration;

			this.textBuffer.Changed += (sender, args) =>
			{
				var snapshot = args.After;
				invokeTagsChanged(snapshot);
			};
		}

      void invokeTagsChanged(ITextSnapshot snapshot)
      {
	      var span = new SnapshotSpan(snapshot, new Span(0, snapshot.Length));
	      TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
      }

		public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			var currentSnapshot = textBuffer.CurrentSnapshot;

			var result =
				from formattedContainer in SqlContainer.Create(currentSnapshot.GetText(), configuration)
				from conformancesChecked in formattedContainer.CheckConformance(configuration.SqlConformanceConfiguration)
				select conformancesChecked;
			if (result.If(out var nonConformances))
				foreach (var nonConformance in nonConformances)
				{
					var (position, length) = nonConformance.Segment;
					var snapshotSpan = new SnapshotSpan(currentSnapshot, new Span(position, length));
					yield return new TagSpan<IErrorTag>(snapshotSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, nonConformance.Message));
				}
		}
	}
}