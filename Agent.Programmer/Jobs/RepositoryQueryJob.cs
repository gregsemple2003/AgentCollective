﻿using Agent.Services;
using System.Threading.Tasks;

namespace Agent.Programmer
{
    public class RepositoryQueryJob : Job
    {
        private readonly RepositoryQueryService _repositoryQueryService;
        private readonly string _localRepoPath;

        public RepositoryQueryJob(RepositoryQueryService repositoryQueryService, string localRepoPath)
        {
            _repositoryQueryService = repositoryQueryService;
            _localRepoPath = localRepoPath;
        }

        public async override Task Run()
        {
            var repositoryQuerySession = _repositoryQueryService.CreateSession(_localRepoPath);

            // your queries go here
            await repositoryQuerySession.PrintFunctionSourceCode("AssetDataStore", "Get");
        }
    }
}
