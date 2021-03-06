﻿//
// DnxProject.cs
//
// Author:
//       Matt Ward <ward.matt@gmail.com>
//
// Copyright (c) 2015 Matthew Ward
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Core.ProgressMonitoring;
using MonoDevelop.Projects;
using OmniSharp.Models;

namespace MonoDevelop.Dnx
{
	public class DnxProject : DotNetAssemblyProject
	{
		AspNet5Project project;
		FilePath fileName;

		public DnxProject ()
			: base ("C#")
		{
		}

		public DnxProject (ProjectCreateInformation info, XmlElement projectOptions)
		{
		}

		public override IEnumerable<string> GetProjectTypes ()
		{
			yield return "DnxProject";
		}

		protected override void OnEndLoad ()
		{
			foreach (string fileName in Directory.GetFiles (BaseDirectory, "*.*", SearchOption.AllDirectories)) {
				if (IsSupportedProjectFileItem (fileName)) {
					Items.Add (CreateFileProjectItem(fileName));
				}
			}
			AddConfigurations ();
			base.OnEndLoad ();
		}

		bool IsSupportedProjectFileItem (string fileName)
		{
			string extension = Path.GetExtension(fileName);
			if (extension.EndsWith ("proj", StringComparison.OrdinalIgnoreCase)) {
				return false;
			} else if (extension.Equals (".sln", StringComparison.OrdinalIgnoreCase)) {
				return false;
			} else if (extension.Equals (".user", StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			return true;
		}

		ProjectFile CreateFileProjectItem (string fileName)
		{
			return new ProjectFile (fileName, GetDefaultBuildAction (fileName));
		}

		public override string GetDefaultBuildAction (string fileName)
		{
			if (IsCSharpFile (fileName)) {
				return BuildAction.Compile;
			}
			return base.GetDefaultBuildAction (fileName);
		}

		static bool IsCSharpFile (string fileName)
		{
			string extension = Path.GetExtension (fileName);
			return String.Equals (".cs", extension, StringComparison.OrdinalIgnoreCase);
		}

		public void AddAssemblyReference (string fileName)
		{
			var projectItem = new ProjectReference (ReferenceType.Assembly, fileName);
			References.Add (projectItem);
		}

		public bool IsCurrentFramework (string framework, IEnumerable<string> frameworks)
		{
			if (CurrentFramework == null) {
				CurrentFramework = frameworks.FirstOrDefault ();
			}

			return CurrentFramework == framework;
		}

		public string CurrentFramework { get; private set; }

		public void UpdateReferences (IEnumerable<string> references)
		{
			var oldReferenceItems = Items.OfType<ProjectReference> ().ToList ();
			Items.RemoveRange (oldReferenceItems);

			foreach (string reference in references) {
				AddAssemblyReference (reference);
			}
		}

		public void Update (AspNet5Project project)
		{
			this.project = project;
		}

		void AddConfigurations ()
		{
			AddConfiguration ("Debug");
			AddConfiguration ("Release");
		}

		void AddConfiguration (string name)
		{
			var configuration = new DnxProjectConfiguration (name);
			Configurations.Add (configuration);
		}

		protected override ExecutionCommand CreateExecutionCommand (ConfigurationSelector configSel, DotNetProjectConfiguration configuration)
		{
			return new DnxProjectExecutionCommand (
				BaseDirectory,
				GetCurrentCommand (),
				DnxServices.ProjectService.CurrentDnxRuntimePath
			);
		}

		protected override bool OnGetSupportsTarget (string target)
		{
			return false;
		}

		string GetCurrentCommand()
		{
			if (project == null)
				return null;
			
			return project.Commands.Keys.FirstOrDefault();
		}

		protected override bool OnGetCanExecute (ExecutionContext context, ConfigurationSelector configuration)
		{
			return true;
		}

		protected override bool OnGetNeedsBuilding (ConfigurationSelector configuration)
		{
			return false;
		}

		protected override BuildResult OnRunTarget (IProgressMonitor monitor, string target, ConfigurationSelector configuration)
		{
			return new BuildResult ();
		}

		protected override void DoExecute (IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configuration)
		{
			var config = GetConfiguration (configuration) as DotNetProjectConfiguration;
			monitor.Log.WriteLine (GettextCatalog.GetString ("Running {0} ...", Name));

			IConsole console = CreateConsole (config, context);
			var aggregatedOperationMonitor = new AggregatedOperationMonitor (monitor);

			try {
				try {
					ExecutionCommand executionCommand = CreateExecutionCommand (configuration, config);
					if (context.ExecutionTarget != null)
						executionCommand.Target = context.ExecutionTarget;

					IProcessAsyncOperation asyncOp = new DnxExecutionHandler ().Execute (executionCommand, console);
					aggregatedOperationMonitor.AddOperation (asyncOp);
					asyncOp.WaitForCompleted ();

					monitor.Log.WriteLine (GettextCatalog.GetString ("The application exited with code: {0}", asyncOp.ExitCode));
				} finally {
					console.Dispose ();
					aggregatedOperationMonitor.Dispose ();
				}
			} catch (Exception ex) {
				LoggingService.LogError (string.Format ("Cannot execute \"{0}\"", Name), ex);
				monitor.ReportError (GettextCatalog.GetString ("Cannot execute \"{0}\"", Name), ex);
			}
		}

		IConsole CreateConsole (DotNetProjectConfiguration config, ExecutionContext context)
		{
			if (config.ExternalConsole)
				return context.ExternalConsoleFactory.CreateConsole (!config.PauseConsoleOutput);
			return context.ConsoleFactory.CreateConsole (!config.PauseConsoleOutput);
		}

		public override FilePath GetOutputFileName (ConfigurationSelector configuration)
		{
			return null;
		}

		/// <summary>
		/// Have to override the SolutionEntityItem otherwise the FileFormat 
		/// changes the file extension back to .csproj when GetValidFileName is called.
		/// This is because the FileFormat finds the DotNetProjectNode for csproj files
		/// when looking at the /MonoDevelop/ProjectModel/MSBuildItemTypes extension.
		/// There does not seem to be a way to insert the DotNetProjectNode for DnxProjects
		/// since these extensions do not have an id.
		/// </summary>
		public override FilePath FileName {
			get {
				return fileName;
			}
			set {
				fileName = value;
				if (ItemHandler.SyncFileName)
					Name = fileName.FileNameWithoutExtension;
				NotifyModified ("FileName");
			}
		}
	}
}

