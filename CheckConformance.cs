using EnvDTE;
using Microsoft.VisualStudio.Shell;
using SqlConformance.Library.SqlContainment;
using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Windows;
using Task = System.Threading.Tasks.Task;

namespace TSqlCop
{
   /// <summary>
   /// Command handler
   /// </summary>
   internal sealed class CheckConformance
   {
      /// <summary>
      /// Command ID.
      /// </summary>
      public const int COMMAND_ID = 0x0100;

      /// <summary>
      /// Command menu group (command set GUID).
      /// </summary>
      public static readonly Guid CommandSet = new Guid("08ca5e3f-8cbd-4aea-9365-af4d22b44d02");

      /// <summary>
      /// VS Package that provides this command, not null.
      /// </summary>
      // ReSharper disable once NotAccessedField.Local
      readonly AsyncPackage package;

      /// <summary>
      /// Initializes a new instance of the <see cref="CheckConformance"/> class.
      /// Adds our command handlers for menu (commands must exist in the command table file)
      /// </summary>
      /// <param name="package">Owner package, not null.</param>
      /// <param name="commandService">Command service to add command to, not null.</param>
      CheckConformance(AsyncPackage package, OleMenuCommandService commandService)
      {
         this.package = package ?? throw new ArgumentNullException(nameof(package));
         commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

         var menuCommandID = new CommandID(CommandSet, COMMAND_ID);
         var menuItem = new MenuCommand(Execute, menuCommandID);
         commandService.AddCommand(menuItem);
      }

      /// <summary>
      /// Gets the instance of the command.
      /// </summary>
      public static CheckConformance Instance { get; set; }

      /// <summary>
      /// Initializes the singleton instance of the command.
      /// </summary>
      /// <param name="package">Owner package, not null.</param>
      public static async Task InitializeAsync(AsyncPackage package)
      {
         // Switch to the main thread - the call to AddCommand in CheckConformance's constructor requires
         // the UI thread.
         await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

         var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
         Instance = new CheckConformance(package, commandService);
      }

      /// <summary>
      /// This function is the callback used to execute the command when the menu item is clicked.
      /// See the constructor to see how the menu item is associated with this function using
      /// OleMenuCommandService service and MenuCommand class.
      /// </summary>
      /// <param name="sender">Event sender.</param>
      /// <param name="e">Event args.</param>
      void Execute(object sender, EventArgs e)
      {
         ThreadHelper.ThrowIfNotOnUIThread();

         var dte = (DTE)ServiceProvider.GlobalProvider.GetService(typeof(DTE));
         if (dte == null)
         {
            MessageBox.Show("DTE service could not be retrieved", "Exception");
            return;
         }

         var textDocument = (TextDocument)dte.ActiveDocument.Object("TextDocument");
         var editPoint = textDocument.StartPoint.CreateEditPoint();
         var text = editPoint.GetText(textDocument.EndPoint);

         var configuration = ConfigurationProvider.Configuration();
         var result =
            from formatted in SqlContainer.Formatted(text, configuration)
            from conformancesChecked in formatted.CheckConformance(configuration.SqlConformanceConfiguration)
            select conformancesChecked;
         if (result.If(out var nonConformances, out var exception))
         {
            var count = nonConformances.Count();
            MessageBox.Show(count.ToString(), "Test");
         }
         else
            MessageBox.Show(exception.Message, "Exception");
      }
   }
}
