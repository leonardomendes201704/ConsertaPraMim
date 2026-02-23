export const CLIENT_PROFILE_TYPES = {
  PF: 1,
  PJ: 2
} as const;

export type ClientProfileTypeValue = typeof CLIENT_PROFILE_TYPES[keyof typeof CLIENT_PROFILE_TYPES];

export interface ClientPjTypeOption {
  value: number;
  label: string;
}

export const CLIENT_PJ_TYPE_OPTIONS: ClientPjTypeOption[] = [
  { value: 1, label: 'Condominio' },
  { value: 2, label: 'Empresa' },
  { value: 3, label: 'Escritorio' },
  { value: 4, label: 'Loja' },
  { value: 5, label: 'Restaurante' },
  { value: 6, label: 'Hotel/Pousada' },
  { value: 7, label: 'Escola/Faculdade' },
  { value: 8, label: 'Clinica/Hospital' },
  { value: 9, label: 'Industria/Fabrica' },
  { value: 10, label: 'Igreja/Templo' },
  { value: 99, label: 'Outros' }
];

export function getClientPjTypeLabel(value?: number | null): string {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) {
    return '';
  }

  return CLIENT_PJ_TYPE_OPTIONS.find((option) => option.value === numeric)?.label || '';
}
