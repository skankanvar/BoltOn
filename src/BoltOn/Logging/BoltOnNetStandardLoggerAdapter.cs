﻿using System;
using Microsoft.Extensions.Logging;

namespace BoltOn.Logging
{
	public interface IBoltOnLogger<TType>
    {
		void Debug(string message);
		void Info(string message);
		void Error(string message);
		void Error(Exception exception);
		void Warn(string message);
    }

	public class BoltOnNetStandardLoggerAdapter<TType> : IBoltOnLogger<TType>
	{
		readonly ILogger<TType> _logger;

		public BoltOnNetStandardLoggerAdapter(ILogger<TType> logger)
		{
			_logger = logger;
		}

		public virtual void Debug(string message)
		{
			_logger.LogDebug(message);
		}

		public virtual void Info(string message)
		{
			_logger.LogInformation(message);
		}

		public virtual void Warn(string message)
		{
			_logger.LogWarning(message);
		}

		public virtual void Error(string message)
		{
			_logger.LogError(message);
		}

		public virtual void Error(Exception exception)
		{
			_logger.LogError(null, exception);
		}
	}
}
