﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BoltOn.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace BoltOn.Bootstrapping
{
	internal sealed class Bootstrapper : IDisposable
	{
		private static Lazy<Bootstrapper> _instance = new Lazy<Bootstrapper>(() => new Bootstrapper());
		private Assembly _callingAssembly;
		private IServiceCollection _serviceCollection;
		private IServiceProvider _serviceProvider;
		private BoltOnOptions _options;
		private bool _isBolted;

		private Bootstrapper()
		{
			Assemblies = new List<Assembly>().AsReadOnly();
			_isBolted = false;
			_serviceCollection = null;
			_serviceProvider = null;
			_options = null;
		}

		internal static Bootstrapper Instance
		{
			get
			{
				return _instance.Value;
			}
		}

		internal IServiceCollection Container
		{
			get
			{
				Check.Requires(_serviceCollection != null, "ServiceCollection not initialized");
				return _serviceCollection;
			}
			set
			{
				_serviceCollection = value;
			}
		}

		internal IServiceProvider ServiceProvider
		{
			get
			{
				Check.Requires(_serviceProvider != null, "ServiceProvider not initialized");
				return _serviceProvider;
			}
		}

		internal IReadOnlyList<Assembly> Assemblies { get; private set; }

		internal void BoltOn(IServiceCollection serviceCollection, BoltOnOptions options, Assembly callingAssembly = null)
		{
			Check.Requires(!_isBolted, "Components are already bolted");
			_isBolted = true;
			_serviceCollection = serviceCollection;
			_options = options;
			_callingAssembly = callingAssembly ?? Assembly.GetCallingAssembly();
			LoadAssemblies();
			RunRegistrationTasks();
		}

		internal void RunPostRegistrationTasks(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
			var context = new PostRegistrationTaskContext(this);
			var postRegistrationTasks = serviceProvider.GetService<IEnumerable<IPostRegistrationTask>>();
			var tasks = serviceProvider.GetServices<IPostRegistrationTask>();
			postRegistrationTasks.ToList().ForEach(t => t.Run(context));
		}

		private void LoadAssemblies()
		{
			var assemblies = new List<Assembly> { Assembly.GetExecutingAssembly(), _callingAssembly };
			var sortedAssemblies = new HashSet<Assembly>();
			assemblies = assemblies.Distinct().ToList();
			assemblies.AddRange(_options.AssembliesToBeIncluded);

			// load assemblies in the order of dependency
			var index = 0;
			while (assemblies.Count != 0)
			{
				var tempRefs = GetReferencedAssemblies(assemblies[index]);
				if (!tempRefs.Intersect(assemblies).Any())
				{
					sortedAssemblies.Add(assemblies[index]);
					assemblies.Remove(assemblies[index]);
					index = 0;
				}
				else
					index += 1;
			}

			Assemblies = sortedAssemblies.ToList().AsReadOnly();

			IEnumerable<Assembly> GetReferencedAssemblies(Assembly assembly)
			{
				var referencedAssemblyNames = assembly.GetReferencedAssemblies();
				foreach (var referencedAssemblyName in referencedAssemblyNames)
				{
					var tempAssembly = Assembly.Load(referencedAssemblyName);
					yield return tempAssembly;
				}
			}
		}

		private void RunRegistrationTasks()
		{
			var registrationTaskType = typeof(IRegistrationTask);
			var registrationTaskTypes = (from a in Assemblies
										 from t in a.GetTypes()
										 where registrationTaskType.IsAssignableFrom(t)
										 && t.IsClass
										 select t).ToList();

			var registrationTaskContext = new RegistrationTaskContext(this);
			foreach (var type in registrationTaskTypes)
			{
				var task = Activator.CreateInstance(type) as IRegistrationTask;
				task.Run(registrationTaskContext);
			}

			RegisterPostRegistrationTasks();
		}

		private void RegisterPostRegistrationTasks()
		{
			var registrationTaskType = typeof(IPostRegistrationTask);
			var registrationTaskTypes = (from a in Assemblies
										 from t in a.GetTypes()
										 where registrationTaskType.IsAssignableFrom(t)
										 && t.IsClass
										 select t).ToList();
			registrationTaskTypes.ForEach(r => _serviceCollection.AddTransient(registrationTaskType, r));
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				_serviceCollection = null;
				_serviceProvider = null;
				Assemblies = null;
				_callingAssembly = null;
				_isBolted = false;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}
