using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ForceSquashAcrossMerges : Popup
    {
        public Models.Commit Target { get; }
        public bool CreateBackup { get; set; } = true;
        public bool AutoStash { get; set; } = true;
        public bool KeepAuthorDate { get; set; }
        public bool AppendMessages { get; set; }
        public string Message { get => _message; set => SetProperty(ref _message, value, true); }

        public ForceSquashAcrossMerges(Repository repo, Models.Commit target)
        {
            _repo = repo;
            Target = target;
            _message = target.Subject;
        }

        public override async Task<bool> Sure()
        {
            _repo.SetWatcherEnabled(false);
            ProgressDescription = "Squashing ...";
            var log = _repo.CreateLog("ForceSquash");
            Use(log);

            var baseSHA = Target.Parents[0];
            var signOff = _repo.Settings.EnableSignOffForCommit;
            var stashName = string.Empty;
            var succ = true;

            if (AutoStash)
            {
                var changes = await new Commands.QueryLocalChanges(_repo.FullPath, false).GetResultAsync();
                foreach (var c in changes)
                {
                    if (c.Index != Models.ChangeState.None)
                    {
                        stashName = $"sourcegit/force-squash/{_repo.CurrentBranch.Head[..7]}";
                        succ = await new Commands.Stash(_repo.FullPath).Use(log).PushAsync(stashName);
                        break;
                    }
                }
                if (!succ)
                {
                    log.Complete();
                    _repo.SetWatcherEnabled(true);
                    return false;
                }
            }

            var backupName = string.Empty;
            if (CreateBackup && _repo.CurrentBranch != null)
            {
                backupName = $"sourcegit/backup/flatten-{Models.Branch.FixName(_repo.CurrentBranch.Name)}-{_repo.CurrentBranch.Head[..7]}";
                succ = await new Commands.Branch(_repo.FullPath, backupName).Use(log).CreateAsync("HEAD", false);
                if (!succ)
                {
                    log.Complete();
                    _repo.SetWatcherEnabled(true);
                    return false;
                }
            }

            succ = await new Commands.Reset(_repo.FullPath, baseSHA, "--soft").Use(log).ExecAsync();
            if (!succ)
            {
                log.Complete();
                _repo.SetWatcherEnabled(true);
                return false;
            }

            var commitMsg = Message;
            if (AppendMessages)
            {
                var commits = await new Commands.QueryCommits(_repo.FullPath, $"{Target.SHA}..HEAD", false).GetResultAsync();
                if (commits.Count > 0)
                {
                    var lines = new List<string>();
                    foreach (var c in commits)
                        lines.Add(c.Subject);
                    commitMsg += "\n\n" + string.Join("\n", lines);
                }
            }

            var commit = new Commands.Commit(_repo.FullPath, commitMsg, signOff, false, false);
            if (KeepAuthorDate)
            {
                var author = Target.Author;
                var date = DateTimeOffset.FromUnixTimeSeconds((long)Target.AuthorTime).ToString("o");
                commit.Args += $" --author={("" + author.Name + " <" + author.Email + ">").Quoted()} --date={date.Quoted()}";
            }
            succ = await commit.Use(log).RunAsync();
            if (!succ)
            {
                log.Complete();
                _repo.SetWatcherEnabled(true);
                return false;
            }

            if (!string.IsNullOrEmpty(stashName))
                await new Commands.Stash(_repo.FullPath).Use(log).PopAsync(stashName);

            log.Complete();
            _repo.SetWatcherEnabled(true);
            _repo.RefreshCommits();
            if (!string.IsNullOrEmpty(backupName))
                App.SendNotification(_repo.FullPath, App.Text("ForceSquash.Success", backupName));
            else
                App.SendNotification(_repo.FullPath, App.Text("ForceSquash.Success", string.Empty));
            return true;
        }

        private readonly Repository _repo;
        private string _message;
    }
}
