﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Microsoft.Azure.Commands.DataLakeAnalytics.Models;
using Microsoft.Azure.Commands.DataLakeAnalytics.Properties;
using Microsoft.Azure.Management.DataLake.AnalyticsJob.Models;
using JobState = Microsoft.Azure.Management.DataLake.AnalyticsJob.Models.JobState;

namespace Microsoft.Azure.Commands.DataLakeAnalytics
{
    [Cmdlet(VerbsCommon.Get, "AzureRmDataLakeAnalyticsJob", DefaultParameterSetName = BaseParameterSetName),
     OutputType(typeof (List<JobInformation>), typeof (JobInformation))]
    public class GetAzureDataLakeAnalyticsJob : DataLakeAnalyticsCmdletBase
    {
        internal const string BaseParameterSetName = "All In Resource Group and Account";
        internal const string JobInfoParameterSetName = "Specific JobInformation";

        [Parameter(ParameterSetName = BaseParameterSetName, ValueFromPipelineByPropertyName = true, Position = 0,
            Mandatory = true,
            HelpMessage =
                "Name of the Data Lake Analytics account name under which want to retrieve the job information.")]
        [Parameter(ParameterSetName = JobInfoParameterSetName, ValueFromPipelineByPropertyName = true, Position = 0,
            Mandatory = true,
            HelpMessage =
                "Name of Data Lake Analytics account name under which want to to retrieve the job information.")]
        [ValidateNotNullOrEmpty]
        [Alias("AccountName")]
        public string Account { get; set; }

        [Parameter(ParameterSetName = JobInfoParameterSetName, ValueFromPipelineByPropertyName = true, Position = 1,
            ValueFromPipeline = true, Mandatory = true,
            HelpMessage = "ID of the specific job to return job information for.")]
        [ValidateNotNullOrEmpty]
        public Guid JobId { get; set; }

        [Parameter(ParameterSetName = JobInfoParameterSetName, ValueFromPipelineByPropertyName = true, Position = 2,
            Mandatory = false, HelpMessage = "Optionally indicates additional job data to include in the job details.")]
        [ValidateNotNullOrEmpty]
        public DataLakeAnalyticsEnums.ExtendedJobData Include { get; set; }

        [Parameter(ParameterSetName = BaseParameterSetName, ValueFromPipelineByPropertyName = true, Position = 1,
            Mandatory = false,
            HelpMessage = "An optional filter which returns jobs with only the specified friendly name.")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; }

        [Parameter(ParameterSetName = BaseParameterSetName, ValueFromPipelineByPropertyName = true, Position = 2,
            Mandatory = false, HelpMessage = "An optional filter which returns jobs only by the specified submitter.")]
        [ValidateNotNullOrEmpty]
        public string Submitter { get; set; }

        [Parameter(ParameterSetName = BaseParameterSetName, ValueFromPipelineByPropertyName = true, Position = 3,
            Mandatory = false,
            HelpMessage = "An optional filter which returns jobs only submitted after the specified time.")]
        [ValidateNotNull]
        public DateTimeOffset? SubmittedAfter { get; set; }

        [Parameter(ParameterSetName = BaseParameterSetName, ValueFromPipelineByPropertyName = true, Position = 4,
            Mandatory = false,
            HelpMessage = "An optional filter which returns jobs only submitted before the specified time.")]
        [ValidateNotNull]
        public DateTimeOffset? SubmittedBefore { get; set; }

        [Parameter(ParameterSetName = BaseParameterSetName, ValueFromPipelineByPropertyName = true, Position = 5,
            Mandatory = false, HelpMessage = "An optional filter which returns jobs with only the specified state.")]
        [ValidateNotNullOrEmpty]
        public JobState[] State { get; set; }

        [Parameter(ParameterSetName = BaseParameterSetName, ValueFromPipelineByPropertyName = true, Position = 6,
            Mandatory = false, HelpMessage = "An optional filter which returns jobs with only the specified state.")]
        [ValidateNotNullOrEmpty]
        public JobResult[] Result { get; set; }

        [Parameter(ParameterSetName = BaseParameterSetName, ValueFromPipelineByPropertyName = true, Position = 3,
            Mandatory = false, HelpMessage = "Name of resource group under which want to retrieve the job information.")
        ]
        [Parameter(ParameterSetName = JobInfoParameterSetName, ValueFromPipelineByPropertyName = true, Position = 7,
            Mandatory = false,
            HelpMessage = "Name of resource group under which want to to retrieve the job information.")]
        [ValidateNotNullOrEmpty]
        public string ResourceGroupName { get; set; }

        public override void ExecuteCmdlet()
        {
            if (JobId != null && JobId != Guid.Empty)
            {
                // Get for single job
                var jobDetails = DataLakeAnalyticsClient.GetJob(ResourceGroupName, Account, JobId);

                if (Include != DataLakeAnalyticsEnums.ExtendedJobData.None)
                {
                    if (!jobDetails.Type.Equals(JobType.USql, StringComparison.InvariantCultureIgnoreCase))
                    {
                        WriteWarningWithTimestamp(string.Format(Resources.AdditionalDataNotSupported, jobDetails.Type));
                    }
                    else
                    {
                        if (Include == DataLakeAnalyticsEnums.ExtendedJobData.All ||
                            Include == DataLakeAnalyticsEnums.ExtendedJobData.DebugInfo)
                        {
                            ((USqlProperties) jobDetails.Properties).DebugData =
                                DataLakeAnalyticsClient.GetDebugDataPaths(ResourceGroupName, Account, JobId);
                        }

                        if (Include == DataLakeAnalyticsEnums.ExtendedJobData.All ||
                            Include == DataLakeAnalyticsEnums.ExtendedJobData.Statistics)
                        {
                            ((USqlProperties) jobDetails.Properties).Statistics =
                                DataLakeAnalyticsClient.GetJobStatistics(ResourceGroupName, Account, JobId);
                        }
                    }
                }

                WriteObject(jobDetails);
            }
            else
            {
                var filter = new List<string>();
                if (!string.IsNullOrEmpty(Submitter))
                {
                    filter.Add(string.Format("submitter eq '{0}'", Submitter));
                }

                if (SubmittedAfter.HasValue)
                {
                    filter.Add(string.Format("submitTime ge datetimeoffset'{0}'", SubmittedAfter.Value.ToString("O")));
                }

                if (SubmittedBefore.HasValue)
                {
                    filter.Add(string.Format("submitTime lt datetimeoffset'{0}'", SubmittedBefore.Value.ToString("O")));
                }

                if (!string.IsNullOrEmpty(Name))
                {
                    filter.Add(string.Format("name eq '{0}'", Name));
                }

                if (State != null && State.Length > 0)
                {
                    filter.Add("(" +
                               string.Join(" or ",
                                   State.Select(state => string.Format("state eq '{0}'", state)).ToArray()) + ")");
                }

                if (Result != null && Result.Length > 0)
                {
                    filter.Add("(" +
                               string.Join(" or ",
                                   Result.Select(result => string.Format("result eq '{0}'", result)).ToArray()) + ")");
                }

                var filterString = string.Join(" and ", filter.ToArray());

                // List all accounts in given resource group if avaliable otherwise all accounts in the subscription
                var list = DataLakeAnalyticsClient.ListJobs(ResourceGroupName, Account,
                    string.IsNullOrEmpty(filterString) ? null : filterString, null, null);
                WriteObject(list, true);
            }
        }
    }
}