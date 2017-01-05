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

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SynchContextSample
{
    /// <summary>
    /// Custom synchronization context implementation using Windows message queue (Win32)
    /// </summary>
    public sealed class MessageQueueSynchronizationContext : SynchronizationContext
    {
        #region Interops for Win32
        private static readonly int WM_QUIT = 0x0012;

        private struct MSG
        {
            public IntPtr hWnd;
            public int msg;
            public IntPtr wParam;
            public IntPtr lParam;
            public IntPtr result;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostThreadMessage(int threadId, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, int wMsgFilterMin, int wMsgFilterMax);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr DispatchMessage([In] ref MSG lpmsg);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();
        #endregion

        /// <summary>
        /// Internal uses Windows message number (Win32).
        /// </summary>
        private static readonly int WM_SC;

        /// <summary>
        /// Type initializer.
        /// </summary>
        static MessageQueueSynchronizationContext()
        {
            // Allocate Windows message number.
            // Using guid because type loaded into multiple AppDomain, type initializer called multiple.
            WM_SC = RegisterWindowMessage("MessageQueueSynchronizationContext_" + Guid.NewGuid().ToString("N"));
        }

        /// <summary>
        /// This synchronization context bound thread id.
        /// </summary>
        private readonly int targetThreadId = GetCurrentThreadId();

        /// <summary>
        /// Number of recursive posts.
        /// </summary>
        private int recursiveCount = 0;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MessageQueueSynchronizationContext()
        {
        }

        /// <summary>
        /// Copy instance.
        /// </summary>
        /// <returns>Copied instance.</returns>
        public override SynchronizationContext CreateCopy()
        {
            return new MessageQueueSynchronizationContext();
        }

        /// <summary>
        /// Send continuation into synchronization context.
        /// </summary>
        /// <param name="continuation">Continuation callback delegate.</param>
        /// <param name="state">Continuation argument.</param>
        public override void Send(SendOrPostCallback continuation, object state)
        {
            this.Post(continuation, state);
        }

        /// <summary>
        /// Post continuation into synchronization context.
        /// </summary>
        /// <param name="continuation">Continuation callback delegate.</param>
        /// <param name="state">Continuation argument.</param>
        public override void Post(SendOrPostCallback continuation, object state)
        {
            // If current thread id is target thread id:
            var currentThreadId = GetCurrentThreadId();
            if (currentThreadId == targetThreadId)
            {
                // HACK: If current thread is already target thread, invoke continuation directly.
                //   But if continuation has invokeing Post/Send recursive, cause stack overflow.
                //   We can fix this problem by simple solution: Continuation invoke every post into queue,
                //   but performance will be lost.
                //   This counter uses post for scattering (each 50 times).
                if (recursiveCount < 50)
                {
                    recursiveCount++;

                    // Invoke continuation on current thread is better performance.
                    continuation(state);

                    recursiveCount--;
                    return;
                }
            }

            // Get continuation and state cookie.
            // Because these values turn to unmanaged value (IntPtr),
            // so GC cannot track instances and maybe collects...
            var continuationCookie = GCHandle.ToIntPtr(GCHandle.Alloc(continuation));
            var stateCookie = GCHandle.ToIntPtr(GCHandle.Alloc(state));

            // Post continuation information into UI queue.
            PostThreadMessage(targetThreadId, WM_SC, continuationCookie, stateCookie);
        }

        /// <summary>
        /// Execute message queue.
        /// </summary>
        public void Run()
        {
            this.Run(null);
        }

        /// <summary>
        /// Execute message queue.
        /// </summary>
        /// <param name="task">Completion awaiting task</param>
        public void Run(Task task)
        {
            // Run only target thread.
            var currentThreadId = GetCurrentThreadId();
            if (currentThreadId != targetThreadId)
            {
                throw new InvalidOperationException();
            }

            // Schedule task completion for abort message loop.
            task?.ContinueWith(_ =>
                PostThreadMessage(targetThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero));

            // Run message loop (very legacy knowledge...)
            while (true)
            {
                // Get front of queue (or waiting).
                MSG msg;
                var result = GetMessage(out msg, IntPtr.Zero, 0, 0);

                // If message number is WM_QUIT (Cause PostQuitMessage API):
                if (result == 0)
                {
                    // Exit.
                    break;
                }

                // If cause error:
                if (result == -1)
                {
                    // Throw.
                    var hr = Marshal.GetHRForLastWin32Error();
                    Marshal.ThrowExceptionForHR(hr);
                }

                // If message is WM_SC:
                if (msg.msg == WM_SC)
                {
                    // Retreive GCHandles from cookies.
                    var continuationHandle = GCHandle.FromIntPtr(msg.wParam);
                    var stateHandle = GCHandle.FromIntPtr(msg.lParam);

                    // Retreive continuation and state.
                    var continuation = (SendOrPostCallback)continuationHandle.Target;
                    var state = stateHandle.Target;

                    // Release handle (Recollectable by GC)
                    continuationHandle.Free();
                    stateHandle.Free();

                    // Invoke continuation.
                    continuation(state);

                    // Consumed message.
                    continue;
                }

                // Translate accelerator (require UI stability)
                TranslateMessage(ref msg);

                // Send to assigned window procedure.
                DispatchMessage(ref msg);
            }
        }
    }
}
