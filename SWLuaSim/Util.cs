using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWLuaSim {
    internal class Util {
        public static T[] ToType<T>(object[] parameters, int max) {
            T[] output = new T[max];

            for (int i = 0; i < Math.Min(max, parameters.Length); i++) {
                if (parameters[i].GetType() == typeof(T)) {
                    output[i] = (T)parameters[i];
                }
            }

            return output;
        }

        public static double[] ToDoubles(object[] parameters, int max) {
            double[] output = new double[max];

            for (int i = 0; i < Math.Min(max, parameters.Length); i++) {
                if (parameters[i].GetType() == typeof(double)) {
                    output[i] = (double)parameters[i];
                } else if (parameters[i].GetType() == typeof(long)) {
                    output[i] = (double)(long)parameters[i];
                }
            }

            return output;
        }
    }
}
