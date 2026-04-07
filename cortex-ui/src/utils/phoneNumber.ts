export interface PhoneCountry {
  code: string;
  label: string;
  dialCode: string;
  placeholder: string;
  format: (digits: string) => string;
}

const digitsOnly = /\D/g;

function limitDigits(value: string, maxLength: number) {
  return value.replace(digitsOnly, "").slice(0, maxLength);
}

function formatUsLikeNumber(value: string) {
  const digits = limitDigits(value, 10);
  if (digits.length <= 3) return digits;
  if (digits.length <= 6) {
    return `(${digits.slice(0, 3)}) ${digits.slice(3)}`;
  }
  return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6)}`;
}

function formatUkNumber(value: string) {
  const digits = limitDigits(value, 10);
  if (digits.length <= 4) return digits;
  if (digits.length <= 7) {
    return `${digits.slice(0, 4)} ${digits.slice(4)}`;
  }
  return `${digits.slice(0, 4)} ${digits.slice(4, 7)} ${digits.slice(7)}`;
}

function formatGrouped(value: string, groupSizes: number[]) {
  const digits = limitDigits(value, groupSizes.reduce((sum, size) => sum + size, 0));
  const groups: string[] = [];
  let index = 0;

  for (const size of groupSizes) {
    const chunk = digits.slice(index, index + size);
    if (!chunk) break;
    groups.push(chunk);
    index += size;
  }

  if (index < digits.length) {
    groups.push(digits.slice(index));
  }

  return groups.join(" ");
}

export const PHONE_COUNTRIES: PhoneCountry[] = [
  {
    code: "US",
    label: "United States",
    dialCode: "+1",
    placeholder: "(555) 123-4567",
    format: formatUsLikeNumber,
  },
  {
    code: "CA",
    label: "Canada",
    dialCode: "+1",
    placeholder: "(555) 123-4567",
    format: formatUsLikeNumber,
  },
  {
    code: "GB",
    label: "United Kingdom",
    dialCode: "+44",
    placeholder: "7400 123 456",
    format: formatUkNumber,
  },
  {
    code: "AU",
    label: "Australia",
    dialCode: "+61",
    placeholder: "412 345 678",
    format: (value) => formatGrouped(value, [3, 3, 3]),
  },
  {
    code: "IN",
    label: "India",
    dialCode: "+91",
    placeholder: "98765 43210",
    format: (value) => formatGrouped(value, [5, 5]),
  },
  {
    code: "DE",
    label: "Germany",
    dialCode: "+49",
    placeholder: "1512 3456789",
    format: (value) => formatGrouped(value, [4, 7]),
  },
  {
    code: "FR",
    label: "France",
    dialCode: "+33",
    placeholder: "6 12 34 56 78",
    format: (value) => formatGrouped(value, [1, 2, 2, 2, 2]),
  },
  {
    code: "MX",
    label: "Mexico",
    dialCode: "+52",
    placeholder: "55 1234 5678",
    format: (value) => formatGrouped(value, [2, 4, 4]),
  },
];

export const DEFAULT_PHONE_COUNTRY = PHONE_COUNTRIES[0];

function getCountriesOrderedByDialCodeLength() {
  return [...PHONE_COUNTRIES].sort(
    (left, right) => right.dialCode.length - left.dialCode.length,
  );
}

export function findPhoneCountryByCode(code: string | undefined) {
  return PHONE_COUNTRIES.find((country) => country.code === code);
}

export function getSuggestedPhoneCountry() {
  if (typeof navigator === "undefined") {
    return DEFAULT_PHONE_COUNTRY;
  }

  const locale = navigator.language || "";
  const region = locale.split("-")[1]?.toUpperCase();

  return findPhoneCountryByCode(region) ?? DEFAULT_PHONE_COUNTRY;
}

export function getPhoneCountryFromValue(value?: string | null) {
  const normalized = value?.trim();
  if (!normalized) {
    return DEFAULT_PHONE_COUNTRY;
  }

  const countries = getCountriesOrderedByDialCodeLength();
  const matchedCountry = countries.find((country) =>
    normalized.startsWith(country.dialCode),
  );

  return matchedCountry ?? DEFAULT_PHONE_COUNTRY;
}

export function getNationalDigits(value?: string | null) {
  const normalized = value?.trim();
  if (!normalized) {
    return "";
  }

  const country = getPhoneCountryFromValue(normalized);
  if (normalized.startsWith(country.dialCode)) {
    return normalized.slice(country.dialCode.length).replace(digitsOnly, "");
  }

  return normalized.replace(digitsOnly, "");
}

export function buildPhoneValue(countryCode: string, nationalNumber: string) {
  const country = findPhoneCountryByCode(countryCode) ?? DEFAULT_PHONE_COUNTRY;
  const digits = nationalNumber.replace(digitsOnly, "");

  if (!digits) {
    return "";
  }

  return `${country.dialCode}${digits}`;
}

export function getFormattedNationalNumber(
  countryCode: string,
  nationalNumber: string,
) {
  const country = findPhoneCountryByCode(countryCode) ?? DEFAULT_PHONE_COUNTRY;
  return country.format(nationalNumber);
}

export function formatStoredPhoneNumber(value?: string | null) {
  if (!value?.trim()) {
    return "—";
  }

  const country = getPhoneCountryFromValue(value);
  const nationalDigits = getNationalDigits(value);
  const formattedNationalNumber = country.format(nationalDigits);

  return formattedNationalNumber
    ? `${country.dialCode} ${formattedNationalNumber}`
    : country.dialCode;
}
