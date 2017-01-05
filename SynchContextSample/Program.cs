/////////////////////////////////////////////////////////////////////////////////////////////////
//
// SynchContext sample codes
// Copyright (c) 2016 Kouji Matsui (@kekyo2)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//	http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
/////////////////////////////////////////////////////////////////////////////////////////////////

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SynchContextSample
{
    class Program
    {
        // Async method entry point.
        private static async Task MainAsync(string[] args)
        {
            using (var fs = new FileStream(
                "Sample.txt",
                FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                1024, FileOptions.Asynchronous))
            {
                var tw = new StreamWriter(fs);

                for (var index = 0; index < 100000; index++)
                {
                    await tw.WriteLineAsync("This is test output by async-await using custom synchronization context...");
                }

                await tw.FlushAsync();
            }
        }

        static void Main(string[] args)
        {
            // Setup Windows message queue based synchronization context.
            //var sc = new MessageQueueSynchronizationContext();

            // Setup BlockingCollection based synchronization context.
            var sc = new QueueSynchronizationContext();

            SynchronizationContext.SetSynchronizationContext(sc);

            // Execute async method.
            // (Usually thread awaited and return from MainAsync...)
            var task = MainAsync(args);

            // Start synchronization context infrastracture.
            sc.Run(task);
        }
    }
}
