﻿#region License
/* Copyright 2016 James F. Bellinger <http://www.zer7.com/software/hidsharp>

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing,
   software distributed under the License is distributed on an
   "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
   KIND, either express or implied.  See the License for the
   specific language governing permissions and limitations
   under the License. */
#endregion

namespace HidSharp
{
    /// <summary>
    /// The priority at which to open a device stream.
    /// </summary>
    public enum OpenPriority
    {
        /// <summary>
        /// The lowest priority.
        /// </summary>
        Idle = -2,

        /// <summary>
        /// Very low priority.
        /// </summary>
        VeryLow = -1,

        /// <summary>
        /// Low priority.
        /// </summary>
        Low = 0,

        /// <summary>
        /// The default priority.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority.
        /// </summary>
        High = 2,

        /// <summary>
        /// The highest priority.
        /// </summary>
        VeryHigh = 3
    }
}
