import { useState } from "react";
import {
  buildPhoneValue,
  DEFAULT_PHONE_COUNTRY,
  findPhoneCountryByCode,
  getFormattedNationalNumber,
  getNationalDigits,
  getPhoneCountryFromValue,
  getSuggestedPhoneCountry,
  PHONE_COUNTRIES,
} from "../utils/phoneNumber";

interface PhoneNumberInputProps {
  id?: string;
  value?: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}

export default function PhoneNumberInput({
  id,
  value,
  onChange,
  disabled = false,
}: PhoneNumberInputProps) {
  const [selectedCountryCode, setSelectedCountryCode] = useState(
    value?.trim()
      ? getPhoneCountryFromValue(value).code
      : getSuggestedPhoneCountry().code,
  );

  const selectedCountry =
    findPhoneCountryByCode(selectedCountryCode) ?? DEFAULT_PHONE_COUNTRY;
  const nationalDigits = getNationalDigits(value);
  const formattedNationalNumber = getFormattedNationalNumber(
    selectedCountry.code,
    nationalDigits,
  );

  const handleCountryChange = (countryCode: string) => {
    const nextCountry = findPhoneCountryByCode(countryCode) ?? DEFAULT_PHONE_COUNTRY;
    setSelectedCountryCode(nextCountry.code);
    onChange(buildPhoneValue(nextCountry.code, nationalDigits));
  };

  const handlePhoneChange = (nextValue: string) => {
    if (nextValue.trim().startsWith("+")) {
      const detectedCountry = getPhoneCountryFromValue(nextValue);
      const detectedDigits = getNationalDigits(nextValue);
      setSelectedCountryCode(detectedCountry.code);
      onChange(buildPhoneValue(detectedCountry.code, detectedDigits));
      return;
    }

    onChange(buildPhoneValue(selectedCountry.code, nextValue));
  };

  return (
    <div className="grid grid-cols-[10rem_1fr] gap-3">
      <select
        value={selectedCountry.code}
        onChange={(event) => handleCountryChange(event.target.value)}
        disabled={disabled}
        className="w-full rounded-md border-gray-300 bg-white pl-3 pr-2 text-gray-900 shadow-sm disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100"
      >
        {PHONE_COUNTRIES.map((country) => (
          <option key={country.code} value={country.code}>
            {country.code} {country.dialCode}
          </option>
        ))}
      </select>

      <input
        id={id}
        type="tel"
        value={formattedNationalNumber}
        onChange={(event) => handlePhoneChange(event.target.value)}
        placeholder={selectedCountry.placeholder}
        disabled={disabled}
        className="w-full rounded-md border-gray-300 bg-white text-gray-900 shadow-sm disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-100 dark:placeholder:text-slate-500"
      />
    </div>
  );
}
