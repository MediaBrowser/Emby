﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Common.ScheduledTasks
{
    /// <summary>
    /// Interface IScheduledTask
    /// </summary>
    public interface IScheduledTask
    {
        /// <summary>
        /// Gets the name of the task
        /// </summary>
        /// <value>The name.</value>
        string Name { get; }

        /// <summary>
        /// Gets the description.
        /// </summary>
        /// <value>The description.</value>
        string Description { get; }

        /// <summary>
        /// Gets the category.
        /// </summary>
        /// <value>The category.</value>
        string Category { get; }

        /// <summary>
        /// Executes the task
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="progress">The progress.</param>
        /// <returns>Task.</returns>
        Task Execute(CancellationToken cancellationToken, IProgress<double> progress);

        /// <summary>
        /// Gets the default triggers.
        /// </summary>
        /// <returns>IEnumerable{BaseTaskTrigger}.</returns>
        IEnumerable<ITaskTrigger> GetDefaultTriggers();

        /// <summary>
        /// Gets the execution options for this task.
        /// </summary>
        /// <value>The execution options for this task.</value>
        TaskExecutionOptions TaskExecutionOptions { get; }
    }
}
