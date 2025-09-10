using System;

namespace GrafikoMat.Core.Scheduling.Engines.Algorithms
{
    [Flags]
    internal enum AvailabilityMask : byte { None = 0, Wants = 2, Available = 4, Any = 7 }

    /// <summary>
    /// Oblicza górne ograniczenie (upper bound) liczby możliwych do przydzielenia dyżurów za pomocą algorytmu max-flow.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    internal static class FlowUpperBound
    {
        // Buduje graf: S -> dni -> lekarze -> T
        public static int Calculate(
             int dayCount, int doctorCount,
             Func<int, int, AvailabilityMask> getAvailabilityMask, // (dzień, lekarz) -> Maska dostępności
             Func<int, int> getRemainingCapacityPerDoctor,      // lekarz -> limit - obciążenie
             Func<int, bool> isDayAllowed)                      // dzień -> czy dopuszczamy
        {
            int nodeCount = 2 + dayCount + doctorCount;
            int sourceNode = dayCount + doctorCount;
            int sinkNode = sourceNode + 1;

            var dinic = new MaxFlowDinic(nodeCount);

            for (int d = 0; d < dayCount; d++)
            {
                if (!isDayAllowed(d)) continue;
                dinic.AddEdge(sourceNode, d, 1); // Pojemność od źródła do dnia = 1
            }

            for (int p = 0; p < doctorCount; p++)
            {
                int capacity = getRemainingCapacityPerDoctor(p);
                if (capacity > 0)
                {
                    dinic.AddEdge(dayCount + p, sinkNode, capacity); // Pojemność od lekarza do ujścia = pozostały limit
                }
            }

            for (int d = 0; d < dayCount; d++)
            {
                if (!isDayAllowed(d)) continue;
                for (int p = 0; p < doctorCount; p++)
                {
                    var mask = getAvailabilityMask(d, p);
                    if (mask != AvailabilityMask.None)
                    {
                        dinic.AddEdge(d, dayCount + p, 1); // Krawędź istnieje, jeśli lekarz jest dostępny
                    }
                }
            }

            return dinic.GetMaxFlow(sourceNode, sinkNode);
        }
    }
}