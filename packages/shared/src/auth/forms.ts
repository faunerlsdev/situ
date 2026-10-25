import { z } from 'zod';

const passwordMessage =
  'La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula y un número.';

export const passwordSchema = z.string().superRefine((value, context) => {
  const valid = value.length >= 8 && /[A-Z]/.test(value) && /[a-z]/.test(value) && /\d/.test(value);
  if (!valid) {
    context.addIssue({ code: z.ZodIssueCode.custom, message: passwordMessage });
  }
});

export const loginSchema = z.object({
  login: z.string().trim().min(1, 'Escribe tu correo o usuario.'),
  password: z.string().min(1, 'Escribe tu contraseña.'),
});

export const registerSchema = z
  .object({
    givenNames: z.string().trim().min(2, 'Escribe tus nombres.').max(80, 'Los nombres son demasiado largos.'),
    surnames: z.string().trim().min(2, 'Escribe tus apellidos.').max(80, 'Los apellidos son demasiado largos.'),
    phone: z.string().trim().min(7, 'Escribe un teléfono válido.').max(20, 'El teléfono es demasiado largo.'),
    userName: z
      .string()
      .trim()
      .regex(
        /^[a-z0-9][a-z0-9_.]{2,29}$/,
        "El usuario debe tener de 3 a 30 caracteres: letras minúsculas, números, '_' o '.'.",
      ),
    email: z.string().trim().email('El correo no es válido.'),
    password: passwordSchema,
    passwordConfirmation: z.string().min(1, 'Confirma la contraseña.'),
    day: z.string().trim().min(1, 'Escribe el día.'),
    month: z.string().trim().min(1, 'Escribe el mes.'),
    year: z.string().trim().min(1, 'Escribe el año.'),
    countryId: z.string().min(1, 'Selecciona un país.'),
    regionId: z.string().min(1, 'Selecciona un departamento.'),
    cityId: z.string().min(1, 'Selecciona una ciudad.'),
    accepted: z.boolean().refine((value) => value, 'Debes aceptar los documentos legales vigentes.'),
    guardianEmail: z.string().trim(),
    minorNoticeAccepted: z.boolean(),
  })
  .superRefine((value, context) => {
    if (value.password !== value.passwordConfirmation) {
      context.addIssue({ code: z.ZodIssueCode.custom, path: ['passwordConfirmation'], message: 'Las contraseñas no coinciden.' });
    }

    if (value.guardianEmail.length > 0) {
      if (!z.string().email().safeParse(value.guardianEmail).success) {
        context.addIssue({ code: z.ZodIssueCode.custom, path: ['guardianEmail'], message: 'El correo no es válido.' });
      } else if (value.guardianEmail.toLowerCase() === value.email.trim().toLowerCase()) {
        context.addIssue({ code: z.ZodIssueCode.custom, path: ['guardianEmail'], message: 'Debe ser un correo distinto al tuyo.' });
      }
      if (!value.minorNoticeAccepted) {
        context.addIssue({ code: z.ZodIssueCode.custom, path: ['minorNoticeAccepted'], message: 'Antes de enviarlo, marca Entendido.' });
      }
    }

    if (!composeDateOfBirth(value.year, value.month, value.day)) {
      context.addIssue({ code: z.ZodIssueCode.custom, path: ['day'], message: 'La fecha de nacimiento no es válida.' });
    }
  });

export const passwordResetRequestSchema = z.object({
  email: z.string().trim().email('El correo no es válido.'),
});

export const resetPasswordSchema = z
  .object({
    password: passwordSchema,
    confirmation: z.string().min(1, 'Repite la contraseña.'),
  })
  .superRefine((value, context) => {
    if (value.password !== value.confirmation) {
      context.addIssue({ code: z.ZodIssueCode.custom, path: ['confirmation'], message: 'Las contraseñas no coinciden.' });
    }
  });

export const guardianDataSchema = z.object({
  fullName: z.string().trim().min(1, 'Escribe el nombre del acudiente.').max(150, 'El nombre es demasiado largo.'),
  documentType: z.enum(['CitizenshipCard', 'ForeignerCard', 'Passport', 'Other'], {
    errorMap: () => ({ message: 'Selecciona el tipo de documento.' }),
  }),
  documentNumber: z.string().trim().min(1, 'Escribe el número de documento.').max(30, 'El documento es demasiado largo.'),
  contactEmail: z.string().trim().email('El correo no es válido.'),
});

export type LoginValues = z.infer<typeof loginSchema>;
export type RegisterValues = z.infer<typeof registerSchema>;
export type PasswordResetRequestValues = z.infer<typeof passwordResetRequestSchema>;
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>;
export type GuardianDataValues = z.infer<typeof guardianDataSchema>;

export function composeDateOfBirth(year: string, month: string, day: string): string | null {
  if (!/^\d{4}$/.test(year) || !/^\d{1,2}$/.test(month) || !/^\d{1,2}$/.test(day)) {
    return null;
  }

  const y = Number(year);
  const m = Number(month);
  const d = Number(day);
  const date = new Date(Date.UTC(y, m - 1, d));
  if (date.getUTCFullYear() !== y || date.getUTCMonth() !== m - 1 || date.getUTCDate() !== d) {
    return null;
  }

  return `${year}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`;
}

/** Display hint for the registration form. The API decides the real account status. */
export function ageInYears(isoDate: string, today = new Date()): number | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(isoDate);
  if (!match) {
    return null;
  }

  const year = Number(match[1]);
  const month = Number(match[2]);
  const day = Number(match[3]);
  let age = today.getFullYear() - year;
  const monthDelta = today.getMonth() + 1 - month;
  if (monthDelta < 0 || (monthDelta === 0 && today.getDate() < day)) {
    age -= 1;
  }

  return age;
}

export const adultAge = 18;

export function lockoutRemainingLabel(retryAfter: string, now = Date.now()): string {
  const end = Date.parse(retryAfter);
  if (Number.isNaN(end)) {
    return 'Demasiados intentos. Espera 15 minutos e inténtalo de nuevo.';
  }

  const remaining = end - now;
  if (remaining <= 0) {
    return 'Ya puedes intentar de nuevo.';
  }

  const totalSeconds = Math.ceil(remaining / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `Podrás intentar de nuevo en ${minutes}:${seconds.toString().padStart(2, '0')}.`;
}
