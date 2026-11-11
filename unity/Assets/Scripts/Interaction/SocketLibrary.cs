using System;
using System.Collections.Generic;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// The sockets an item can be placed in, with one interaction layer name each, so a probe can never snap into a stand slot (T-IN4 and the plan's socket library).
    /// The names are the ones the Blender recipes give their SOCKET_ empties (SOCKET_Finger, SOCKET_Arm, ...).
    /// </summary>
    public static class SocketLibrary
    {
        public const string Finger = "finger", Arm = "arm", Chest = "chest", Wrist = "wrist", Ear = "ear", Face = "face", Stand = "stand", Trolley = "trolley", GivingSet = "giving_set";

        private static readonly Dictionary<string, string> Layers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { Finger, "Socket.Finger" }, { Arm, "Socket.Arm" }, { Chest, "Socket.Chest" }, { Wrist, "Socket.Wrist" }, { Ear, "Socket.Ear" },
            { Face, "Socket.Face" }, { Stand, "Socket.Stand" }, { Trolley, "Socket.Trolley" }, { GivingSet, "Socket.GivingSet" }
        };

        public static IEnumerable<string> Names => Layers.Keys;
        public static bool IsKnown(string socket) => socket != null && Layers.ContainsKey(socket);

        /// <summary>The interaction layer name of a socket; an item and a socket interact only when they share it.</summary>
        public static string LayerOf(string socket) => IsKnown(socket) ? Layers[socket] : throw new ArgumentException("unknown socket " + socket);

        /// <summary>The Blender point name for a socket, for example "SOCKET_Finger".</summary>
        public static string PointName(string socket) => "SOCKET_" + char.ToUpperInvariant(socket[0]) + socket.Substring(1);
    }

    /// <summary>
    /// One socket and what is in it: only one item at a time (T-IN4). A second item offered while the first is in is refused and does nothing; when the first leaves, the next may enter.
    /// </summary>
    public sealed class SocketSlot
    {
        public SocketSlot(string kind)
        {
            if (!SocketLibrary.IsKnown(kind)) throw new ArgumentException("unknown socket " + kind);
            Kind = kind;
        }

        public string Kind { get; }
        public string Occupant { get; private set; } = "";
        public bool IsFree => Occupant.Length == 0;

        /// <summary>Raised when an item snaps in (a view plays the haptic pulse and the click).</summary>
        public event Action<string> Snapped;

        public bool TryOccupy(string item)
        {
            if (string.IsNullOrEmpty(item) || !IsFree) return false;
            Occupant = item;
            Snapped?.Invoke(item);
            return true;
        }

        public void Release(string item)
        {
            if (Occupant == item) Occupant = "";
        }
    }
}
