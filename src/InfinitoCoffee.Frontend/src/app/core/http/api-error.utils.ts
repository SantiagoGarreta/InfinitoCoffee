import { HttpErrorResponse } from '@angular/common/http';

import { ValidationProblemDetails } from './problem-details.model';

export function toUserMessage(error: unknown, fallbackMessage: string): string {
  if (!(error instanceof HttpErrorResponse)) {
    return fallbackMessage;
  }

  if (error.status === 0) {
    return 'No fue posible conectar con la API. Intenta nuevamente.';
  }

  const problem = error.error as ValidationProblemDetails | string | null;

  if (typeof problem === 'string' && problem.trim().length > 0) {
    return problem;
  }

  if (problem && typeof problem === 'object') {
    const validationMessage = firstValidationMessage(problem.errors);
    if (validationMessage) {
      return validationMessage;
    }

    if (problem.detail?.trim()) {
      return problem.detail;
    }

    if (problem.title?.trim()) {
      return problem.title;
    }
  }

  switch (error.status) {
    case 400:
      return 'La solicitud no es válida.';
    case 404:
      return 'No se encontró el recurso solicitado.';
    case 409:
      return 'El recurso ya existe o entró en conflicto con el estado actual.';
    default:
      return fallbackMessage;
  }
}

function firstValidationMessage(errors: Record<string, string[]> | undefined): string | null {
  if (!errors) {
    return null;
  }

  for (const value of Object.values(errors)) {
    if (value.length > 0) {
      return value[0] ?? null;
    }
  }

  return null;
}
