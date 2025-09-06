using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GrafikoMat.ViewModels
{
    public class DoctorEditorViewModel : ObservableObject
    {
        public DoctorProfile Profile { get; }
        public ObservableCollection<UnitAssignmentViewModel> Assignments { get; } = new();

        #region Właściwości-opakowania z logiką

        public string FirstName
        {
            get => Profile.FirstName;
            set
            {
                if (Profile.FirstName != value)
                {
                    Profile.FirstName = SanitizeName(value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid)); // Do walidacji
                }
            }
        }

        public string LastName
        {
            get => Profile.LastName;
            set
            {
                if (Profile.LastName != value)
                {
                    Profile.LastName = SanitizeName(value);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid)); // Do walidacji
                    GenerateAbbreviation();
                }
            }
        }

        public string Email
        {
            get => Profile.Email;
            set
            {
                if (Profile.Email != value)
                {
                    Profile.Email = value.Trim(); // Tylko czyscimy spacje
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid)); // Do walidacji
                }
            }
        }

        public string Abbreviation
        {
            get => Profile.Abbreviation;
            set
            {
                if (Profile.Abbreviation != value)
                {
                    Profile.Abbreviation = value.Trim().ToUpper();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid)); // Do walidacji
                }
            }
        }

        #endregion

        public DoctorEditorViewModel(DoctorProfile profile, List<Unit> allUnits, List<UnitDoctorAssignment> currentAssignments)
        {
            Profile = profile;

            foreach (var unit in allUnits.OrderBy(u => u.Name))
            {
                var assignment = currentAssignments.FirstOrDefault(a => a.UnitId == unit.Id);
                Assignments.Add(new UnitAssignmentViewModel(unit, assignment != null, assignment?.IsActive ?? true));
            }
        }

        private string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var sb = new StringBuilder();
            var parts = name.Trim().Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length > 0)
                {
                    sb.Append(char.ToUpper(part[0]) + part.Substring(1).ToLower());
                }
                // Logika do odtworzenia myślników (uproszczona)
                if (i < parts.Length - 1 && name.Contains(parts[i] + "-"))
                {
                    sb.Append('-');
                }
                else if (i < parts.Length - 1)
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString();
        }

        private void GenerateAbbreviation()
        {
            if (LastName.Length >= 3)
            {
                Abbreviation = LastName.Substring(0, 3).ToUpper();
            }
            else
            {
                Abbreviation = LastName.ToUpper().PadRight(3, 'X');
            }
        }

        // Właściwość do walidacji (użyjemy w punkcie 3 i 5)
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(FirstName) &&
            !string.IsNullOrWhiteSpace(LastName) &&
            !string.IsNullOrWhiteSpace(Abbreviation) &&
            Abbreviation.Length == 3 &&
            IsValidEmail(Email);

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                return Regex.IsMatch(email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}