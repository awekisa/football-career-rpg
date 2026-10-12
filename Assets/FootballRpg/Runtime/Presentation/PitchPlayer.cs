using UnityEngine;

namespace FootballRpg.Presentation
{
    public sealed class PitchPlayer : MonoBehaviour
    {
        public string PlayerId { get; private set; }
        public int ShirtNumber { get; private set; }
        public bool IsHome { get; private set; }
        public bool IsUser { get; private set; }
        private GameObject selectionRing;

        public void Initialize(string id, int number, bool home, bool user, GameObject ring)
        {
            PlayerId = id;
            ShirtNumber = number;
            IsHome = home;
            IsUser = user;
            selectionRing = ring;
            selectionRing.SetActive(false);
        }

        public void SetSelected(bool selected) => selectionRing.SetActive(selected);
    }
}
