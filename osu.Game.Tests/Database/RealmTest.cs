// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Models;
using osu.Game.Rulesets;

#nullable enable

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public abstract class RealmTest
    {
        private static readonly TemporaryNativeStorage storage;

        static RealmTest()
        {
            storage = new TemporaryNativeStorage("realm-test");
            storage.DeleteDirectory(string.Empty);
        }

        protected void RunTestWithRealm(Action<RealmAccess, OsuStorage> testAction, [CallerMemberName] string caller = "")
        {
            using (HeadlessGameHost host = new CleanRunHeadlessGameHost(callingMethodName: caller))
            {
                host.Run(new RealmTestGame(() =>
                {
                    // ReSharper disable once AccessToDisposedClosure
                    var testStorage = new OsuStorage(host, storage.GetStorageForDirectory(caller));

                    using (var realm = new RealmAccess(testStorage, "client"))
                    {
                        Logger.Log($"Running test using realm file {testStorage.GetFullPath(realm.Filename)}");
                        testAction(realm, testStorage);

                        realm.Dispose();

                        Logger.Log($"Final database size: {getFileSize(testStorage, realm)}");
                        realm.Compact();
                        Logger.Log($"Final database size after compact: {getFileSize(testStorage, realm)}");
                    }
                }));
            }
        }

        protected void RunTestWithRealmAsync(Func<RealmAccess, Storage, Task> testAction, [CallerMemberName] string caller = "")
        {
            using (HeadlessGameHost host = new CleanRunHeadlessGameHost(callingMethodName: caller))
            {
                host.Run(new RealmTestGame(async () =>
                {
                    var testStorage = storage.GetStorageForDirectory(caller);

                    using (var realm = new RealmAccess(testStorage, "client"))
                    {
                        Logger.Log($"Running test using realm file {testStorage.GetFullPath(realm.Filename)}");
                        await testAction(realm, testStorage);

                        realm.Dispose();

                        Logger.Log($"Final database size: {getFileSize(testStorage, realm)}");
                        realm.Compact();
                    }
                }));
            }
        }

        protected static BeatmapSetInfo CreateBeatmapSet(RulesetInfo ruleset)
        {
            RealmFile createRealmFile() => new RealmFile { Hash = Guid.NewGuid().ToString().ComputeSHA2Hash() };

            var metadata = new BeatmapMetadata
            {
                Title = "My Love",
                Artist = "Kuba Oms"
            };

            var beatmapSet = new BeatmapSetInfo
            {
                Beatmaps =
                {
                    new BeatmapInfo(ruleset, new BeatmapDifficulty(), metadata) { DifficultyName = "Easy", },
                    new BeatmapInfo(ruleset, new BeatmapDifficulty(), metadata) { DifficultyName = "Normal", },
                    new BeatmapInfo(ruleset, new BeatmapDifficulty(), metadata) { DifficultyName = "Hard", },
                    new BeatmapInfo(ruleset, new BeatmapDifficulty(), metadata) { DifficultyName = "Insane", }
                },
                Files =
                {
                    new RealmNamedFileUsage(createRealmFile(), "test [easy].osu"),
                    new RealmNamedFileUsage(createRealmFile(), "test [normal].osu"),
                    new RealmNamedFileUsage(createRealmFile(), "test [hard].osu"),
                    new RealmNamedFileUsage(createRealmFile(), "test [insane].osu"),
                }
            };

            for (int i = 0; i < 8; i++)
                beatmapSet.Files.Add(new RealmNamedFileUsage(createRealmFile(), $"hitsound{i}.mp3"));

            foreach (var b in beatmapSet.Beatmaps)
                b.BeatmapSet = beatmapSet;

            return beatmapSet;
        }

        protected static RulesetInfo CreateRuleset() =>
            new RulesetInfo(0, "osu!", "osu", true);

        private class RealmTestGame : Framework.Game
        {
            public RealmTestGame(Func<Task> work)
            {
                // ReSharper disable once AsyncVoidLambda
                Scheduler.Add(async () =>
                {
                    await work().ConfigureAwait(true);
                    Exit();
                });
            }

            public RealmTestGame(Action work)
            {
                Scheduler.Add(() =>
                {
                    work();
                    Exit();
                });
            }
        }

        private static long getFileSize(Storage testStorage, RealmAccess realm)
        {
            try
            {
                using (var stream = testStorage.GetStream(realm.Filename))
                    return stream?.Length ?? 0;
            }
            catch
            {
                // windows runs may error due to file still being open.
                return 0;
            }
        }
    }
}
