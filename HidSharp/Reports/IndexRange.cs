﻿#region License
/* Copyright 2011, 2018 James F. Bellinger <http://www.zer7.com/software/hidsharp>

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

using System.Collections.Generic;

namespace HidSharp.Reports
{
    public class IndexRange : Indexes
    {
        public IndexRange()
        {

        }

        public IndexRange(uint minimum, uint maximum)
        {
            Minimum = minimum; Maximum = maximum;
        }

        public override bool TryGetIndexFromValue(uint value, out int index)
        {
            if (value >= Minimum && value <= Maximum)
            {
                index = (int)(value - Minimum); return true;
            }

            return base.TryGetIndexFromValue(value, out index);
        }

        public override IEnumerable<uint> GetValuesFromIndex(int index)
        {
            if (index < 0 || index >= Count) { yield break; }
            yield return (uint)(Minimum + index);
        }

        public override int Count
        {
            get { return (int)(Maximum - Minimum + 1); }
        }

        public uint Minimum
        {
            get;
            set;
        }

        public uint Maximum
        {
            get;
            set;
        }
    }
}
