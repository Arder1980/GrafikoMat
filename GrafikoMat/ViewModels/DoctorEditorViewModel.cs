using GrafikoMat.Common;
using GrafikoMat.Core.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace GrafikoMat.ViewModels
{
    public class DoctorEditorViewModel : ObservableObject
    {
        public DoctorProfile Profile { get; }
        public ObservableCollection<UnitAssignmentViewModel> Assignments { get; } = new();
        private readonly HashSet<string> _existingAbbreviations;
        public bool IsNewDoctor => Profile.Id == Guid.Empty;

        private bool _showPasswordSection;
        public bool ShowPasswordSection { get => _showPasswordSection; set => SetProperty(ref _showPasswordSection, value); }

        private bool _showResetButton;
        public bool ShowResetButton { get => _showResetButton; set => SetProperty(ref _showResetButton, value); }

        #region Właściwości-opakowania z logiką

        public string FirstName
        {
            get => Profile.FirstName;
            set
            {
                if (Profile.FirstName != value)
                {
                    Profile.FirstName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(NameErrorMessage));
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
                    Profile.LastName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        GenerateAbbreviation();
                    }
                    OnPropertyChanged(nameof(NameErrorMessage));
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
                    Profile.Email = value.Trim();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(EmailErrorMessage));
                }
            }
        }

        public string Abbreviation
        {
            get => Profile.Abbreviation;
            set
            {
                var upperValue = value.Trim().ToUpper();
                if (Profile.Abbreviation != upperValue)
                {
                    Profile.Abbreviation = upperValue;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(AbbreviationErrorMessage));
                }
            }
        }

        private string _password = string.Empty;
        public string Password { get => _password; set => SetProperty(ref _password, value); }
        #endregion

        #region Właściwości dla komunikatów o błędach

        public string NameErrorMessage
        {
            get
            {
                var validNameRegex = new Regex(@"^[\p{L}\s-]*$");
                if (!validNameRegex.IsMatch(FirstName) || !validNameRegex.IsMatch(LastName))
                {
                    return "Imię i nazwisko mogą zawierać tylko litery, spacje i myślniki.";
                }
                return string.Empty;
            }
        }

        public string AbbreviationErrorMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Abbreviation)) return "Skrót jest wymagany.";
                if (Abbreviation.Length != 3) return "Skrót musi mieć 3 znaki.";
                if (_existingAbbreviations.Contains(Abbreviation)) return "Ten skrót jest już używany.";
                return string.Empty;
            }
        }

        public string EmailErrorMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Email)) return "Email jest wymagany.";
                if (!IsValidEmail(Email)) return "Niepoprawny format adresu email.";
                return string.Empty;
            }
        }
        #endregion

        public DoctorEditorViewModel(DoctorProfile profile, List<Unit> allUnits, List<UnitDoctorAssignment> currentAssignments, IEnumerable<string> existingAbbreviations)
        {
            Profile = profile;
            _existingAbbreviations = new HashSet<string>(existingAbbreviations, StringComparer.OrdinalIgnoreCase);

            if (IsNewDoctor)
            {
                Password = PasswordGenerator.GenerateInitialPassword();
                ShowPasswordSection = true;
                ShowResetButton = false;
            }
            else
            {
                ShowPasswordSection = false;
                ShowResetButton = true;
            }

            foreach (var unit in allUnits.OrderBy(u => u.Name))
            {
                var assignment = currentAssignments.FirstOrDefault(a => a.UnitId == unit.Id);

                // ZMIANA: Przekazujemy 'assignment != null' jako informację,
                // że powiązanie już istnieje w bazie danych.
                Assignments.Add(new UnitAssignmentViewModel(unit,
                    isAssigned: assignment != null,
                    isActive: assignment?.IsActive ?? true,
                    isPersisted: assignment != null));
            }
        }

        public void SetNewGeneratedPassword(string newPassword)
        {
            Password = newPassword;
            ShowPasswordSection = true;
            ShowResetButton = false;
        }

        public string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var cleaned = name.Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            cleaned = Regex.Replace(cleaned, @"\s*-\s*", "-");
            cleaned = Regex.Replace(cleaned, "-+", "-");
            var parts = cleaned.Split(' ');
            var resultParts = parts.Select(part =>
            {
                var subParts = part.Split('-');
                var resultSubParts = subParts.Select(subPart =>
                {
                    if (string.IsNullOrEmpty(subPart)) return "";
                    return char.ToUpper(subPart[0]) + subPart.Substring(1).ToLower();
                });
                return string.Join("-", resultSubParts);
            });
            return string.Join(" ", resultParts);
        }

        private void GenerateAbbreviation()
        {
            if (string.IsNullOrWhiteSpace(LastName)) return;
            var baseName = LastName.Split(new[] { ' ', '-' })[0];
            string newAbbreviation;
            if (baseName.Length >= 3)
            {
                newAbbreviation = baseName.Substring(0, 3).ToUpper();
            }
            else
            {
                newAbbreviation = baseName.ToUpper().PadRight(3, 'X');
            }
            Abbreviation = newAbbreviation;
        }

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(FirstName)
                    && !string.IsNullOrWhiteSpace(LastName)
                    && string.IsNullOrEmpty(NameErrorMessage)
                    && string.IsNullOrEmpty(AbbreviationErrorMessage)
                    && string.IsNullOrEmpty(EmailErrorMessage);
            }
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try
            {
                return Regex.IsMatch(email,
                    @"^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}