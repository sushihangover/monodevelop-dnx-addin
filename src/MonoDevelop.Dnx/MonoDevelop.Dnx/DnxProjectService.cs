﻿//
// DnxProjectService.cs
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
using Microsoft.CodeAnalysis;
using MonoDevelop.Dnx.Omnisharp;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using OmniSharp.AspNet5;
using OmniSharp.Models;

namespace MonoDevelop.Dnx
{
	public class DnxProjectService
	{
		AspNet5Context context;
		MonoDevelopApplicationLifetime applicationLifetime;

		public DnxProjectService ()
		{
		}

		public void Initialize ()
		{
			IdeApp.Workspace.SolutionLoaded += SolutionLoaded;
			IdeApp.Workspace.SolutionUnloaded += SolutionUnloaded;
		}

		void SolutionUnloaded (object sender, SolutionEventArgs e)
		{
			UnloadProjectSystem ();
		}

		void UnloadProjectSystem ()
		{
			if (applicationLifetime != null) {
				applicationLifetime.Stopping ();
				applicationLifetime.Dispose ();
				applicationLifetime = null;
				context = null;
			}
		}

		void SolutionLoaded (object sender, SolutionEventArgs e)
		{
			try {
				if (e.Solution.HasAspNetProjects ()) {
					LoadAspNetProjectSystem (e.Solution);
				}
			} catch (Exception ex) {
				MessageService.ShowError (ex.Message);
			}
		}

		void LoadAspNetProjectSystem (Solution solution)
		{
			applicationLifetime = new MonoDevelopApplicationLifetime ();
			context = new AspNet5Context ();
			var factory = new AspNet5ProjectSystemFactory ();
			var projectSystem = factory.CreateProjectSystem (solution, applicationLifetime, context);
			projectSystem.Initalize ();
		}

		public void OnReferencesUpdated (ProjectId projectId, FrameworkProject frameworkProject)
		{
			DispatchService.GuiDispatch (()  => {
				var maintainer = new AspNetProjectReferenceMaintainer (context);
				maintainer.UpdateReferences (projectId, frameworkProject);
			});
		}

		public void OnProjectChanged (AspNet5Project project)
		{
			DispatchService.GuiDispatch (() => UpdateProject (project));
		}

		void UpdateProject (AspNet5Project project)
		{
			Solution solution = IdeApp.ProjectOperations.CurrentSelectedSolution;
			if (solution == null)
				return;

			DnxProject matchedProject = solution.FindProjectByProjectJsonFileName (project.Path);
			if (matchedProject != null) {
				matchedProject.Update (project);
			} else {
				LoggingService.LogWarning (String.Format("Unable to find project by json file. '{0}'", project.Path));
			}
		}

		public string CurrentDnxRuntimePath {
			get {
				if (context != null) {
					return context.RuntimePath;
				}
				return null;
			}
		}
	}
}

