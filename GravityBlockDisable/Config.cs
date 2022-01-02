using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GravityBlockDisable
{
    public class Config
    {
        public int SecondsBetweenGravChecks = 300;

        public List<String> BlockPairNamesToDisableOutOfGrav = new List<string>();
    }
}
