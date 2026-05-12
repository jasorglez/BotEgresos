import { CommonModule } from '@angular/common';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

type HealthStatus = 'checking' | 'ok' | 'error';
type SubmitState = 'idle' | 'submitting' | 'success' | 'error';
type ActivationState = 'idle' | 'preparing' | 'ready' | 'launching' | 'error';
type ChannelType = 'TELEGRAM' | 'WHATSAPP';

interface PlanCard {
  name: string;
  price: string;
  description: string;
  features: string[];
  paymentUrl: string;
  featured?: boolean;
}

interface RegisterTenantRequest {
  companyName: string;
  slug: string | null;
  email: string;
  phone: string | null;
  firstName: string;
  lastName: string | null;
  password: string;
}

interface RegisterTenantResponse {
  tenantId: number | string;
  tenantPublicId: string;
  companyName: string;
  slug: string;
  userId: number | string;
  userPublicId: string;
  email: string;
  roleCode: string;
}

interface LoginRequest {
  email: string;
  password: string;
  tenantSlug: string | null;
}

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  userId: number;
  email: string;
  tenantId: number;
  tenantName: string;
  tenantSlug: string;
  roleCode: string;
}

interface CreateBotLinkCodeRequest {
  channel: ChannelType;
}

interface CreateBotLinkCodeResponse {
  channel: ChannelType;
  linkCode: string;
  expiresAtUtc: string;
}

interface ChannelLaunchState {
  telegramCode: string | null;
  telegramUrl: string | null;
  telegramExpiresAtUtc: string | null;
  whatsappCode: string | null;
  whatsappUrl: string | null;
  whatsappExpiresAtUtc: string | null;
}

interface HealthResponse {
  status: string;
  service: string;
  utc: string;
}

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = 'https://endpoints.biapp.com.mx/egresosbot';
  private readonly telegramBotUsername = 'EgresosBi2OficialBot';
  private readonly whatsappBotNumber = '';

  protected readonly whatsappEnabled = this.whatsappBotNumber.length > 0;
  protected readonly healthStatus = signal<HealthStatus>('checking');
  protected readonly healthMessage = signal('Validando API...');
  protected readonly submitState = signal<SubmitState>('idle');
  protected readonly submitMessage = signal('');
  protected readonly createdTenant = signal<RegisterTenantResponse | null>(null);
  protected readonly activationState = signal<ActivationState>('idle');
  protected readonly activationMessage = signal('');
  protected readonly authSession = signal<LoginResponse | null>(null);
  protected readonly channelLaunchState = signal<ChannelLaunchState>({
    telegramCode: null,
    telegramUrl: null,
    telegramExpiresAtUtc: null,
    whatsappCode: null,
    whatsappUrl: null,
    whatsappExpiresAtUtc: null
  });

  protected readonly plans: PlanCard[] = [
    {
      name: 'Inicio',
      price: '$199 MXN',
      description: 'Para negocios que quieren capturar egresos desde WhatsApp o Telegram sin hojas de calculo.',
      features: [
        '1 tenant y 1 propietario',
        'Registro de egresos desde bots',
        'Categorias base y salud de cuenta',
        'Acceso al panel de activacion'
      ],
      paymentUrl: '#checkout'
    },
    {
      name: 'Operacion',
      price: '$399 MXN',
      description: 'Para equipos pequenos que necesitan control mas constante y mejor adopcion.',
      features: [
        'Todo lo de Inicio',
        'Mas usuarios y enlaces de bot',
        'Soporte prioritario',
        'Ideal para crecimiento inicial'
      ],
      paymentUrl: '#checkout',
      featured: true
    },
    {
      name: 'Escala',
      price: 'Cotizacion',
      description: 'Para empresas con mayor volumen, multiples responsables o necesidad de soporte dedicado.',
      features: [
        'Onboarding asistido',
        'Ajustes por tenant',
        'Acompanamiento en activacion',
        'Preparado para integraciones futuras'
      ],
      paymentUrl: 'mailto:admin@empresa-demo.com?subject=Plan%20Escala%20EgresosBot'
    }
  ];

  protected readonly onboardingForm: RegisterTenantRequest = {
    companyName: '',
    slug: '',
    email: '',
    phone: '',
    firstName: '',
    lastName: '',
    password: ''
  };

  ngOnInit(): void {
    this.checkHealth();
  }

  protected checkHealth(): void {
    this.healthStatus.set('checking');
    this.healthMessage.set('Validando API...');

    this.http.get<HealthResponse>(`${this.apiBaseUrl}/api/health`).subscribe({
      next: (response) => {
        this.healthStatus.set('ok');
        this.healthMessage.set(`${response.service} responde ${response.status} (${response.utc})`);
      },
      error: () => {
        this.healthStatus.set('error');
        this.healthMessage.set('La API no respondio. Revisa nginx, SSL o el contenedor.');
      }
    });
  }

  protected submitOnboarding(): void {
    this.submitState.set('submitting');
    this.submitMessage.set('');
    this.createdTenant.set(null);
    this.activationState.set('idle');
    this.activationMessage.set('');
    this.authSession.set(null);
    this.channelLaunchState.set({
      telegramCode: null,
      telegramUrl: null,
      telegramExpiresAtUtc: null,
      whatsappCode: null,
      whatsappUrl: null,
      whatsappExpiresAtUtc: null
    });

    const payload: RegisterTenantRequest = {
      companyName: this.onboardingForm.companyName.trim(),
      slug: this.toNullable(this.onboardingForm.slug),
      email: this.onboardingForm.email.trim(),
      phone: this.toNullable(this.onboardingForm.phone),
      firstName: this.onboardingForm.firstName.trim(),
      lastName: this.toNullable(this.onboardingForm.lastName),
      password: this.onboardingForm.password
    };

    this.http.post<RegisterTenantResponse>(`${this.apiBaseUrl}/api/onboarding/register`, payload).subscribe({
      next: (response) => {
        this.createdTenant.set(response);
        this.submitState.set('success');
        this.submitMessage.set(`Cuenta creada: ${response.companyName} (${response.slug}).`);
        this.prepareChannelActivation(response.slug, payload.email, payload.password);
        this.resetForm();
      },
      error: (error) => {
        const message =
          error?.error?.detail ??
          error?.error?.title ??
          'No se pudo completar el onboarding.';

        this.submitState.set('error');
        this.submitMessage.set(message);
      }
    });
  }

  protected connectChannel(channel: ChannelType): void {
    const session = this.authSession();
    if (!session) {
      this.activationState.set('error');
      this.activationMessage.set('No pudimos preparar tu sesion. Recarga la pagina o inicia de nuevo el registro.');
      return;
    }

    if (channel === 'WHATSAPP' && !this.whatsappBotNumber) {
      this.activationState.set('error');
      this.activationMessage.set('WhatsApp aun no esta configurado. Telegram ya esta listo para conectar.');
      return;
    }

    const cachedUrl = channel === 'TELEGRAM'
      ? this.channelLaunchState().telegramUrl
      : this.channelLaunchState().whatsappUrl;

    if (cachedUrl) {
      this.activationState.set('ready');
      this.activationMessage.set(channel === 'TELEGRAM'
        ? 'Abriendo Telegram con tu codigo precargado.'
        : 'Abriendo WhatsApp con tu codigo precargado.');
      this.openChannel(cachedUrl);
      return;
    }

    this.activationState.set('launching');
    this.activationMessage.set(channel === 'TELEGRAM'
      ? 'Preparando Telegram con tu codigo de acceso...'
      : 'Preparando WhatsApp con tu codigo de acceso...');

    const headers = new HttpHeaders({
      Authorization: `Bearer ${session.accessToken}`
    });

    this.http.post<CreateBotLinkCodeResponse>(
      `${this.apiBaseUrl}/api/bot-links/link-code`,
      { channel } satisfies CreateBotLinkCodeRequest,
      { headers }
    ).subscribe({
      next: (response) => {
        const launchUrl = this.buildChannelUrl(channel, response.linkCode);
        const current = this.channelLaunchState();

        this.channelLaunchState.set({
          ...current,
          telegramCode: channel === 'TELEGRAM' ? response.linkCode : current.telegramCode,
          telegramUrl: channel === 'TELEGRAM' ? launchUrl : current.telegramUrl,
          telegramExpiresAtUtc: channel === 'TELEGRAM' ? response.expiresAtUtc : current.telegramExpiresAtUtc,
          whatsappCode: channel === 'WHATSAPP' ? response.linkCode : current.whatsappCode,
          whatsappUrl: channel === 'WHATSAPP' ? launchUrl : current.whatsappUrl,
          whatsappExpiresAtUtc: channel === 'WHATSAPP' ? response.expiresAtUtc : current.whatsappExpiresAtUtc
        });

        this.activationState.set('ready');
        this.activationMessage.set(channel === 'TELEGRAM'
          ? 'Telegram listo. Si no se abre automaticamente, usa el boton o copia el codigo de respaldo.'
          : 'WhatsApp listo. Si no se abre automaticamente, usa el boton o copia el codigo de respaldo.');
        this.openChannel(launchUrl);
      },
      error: (error) => {
        const message =
          error?.error?.message ??
          error?.error?.detail ??
          error?.error?.title ??
          'No se pudo generar el codigo de activacion del canal.';

        this.activationState.set('error');
        this.activationMessage.set(message);
      }
    });
  }

  private resetForm(): void {
    this.onboardingForm.companyName = '';
    this.onboardingForm.slug = '';
    this.onboardingForm.email = '';
    this.onboardingForm.phone = '';
    this.onboardingForm.firstName = '';
    this.onboardingForm.lastName = '';
    this.onboardingForm.password = '';
  }

  private toNullable(value: string | null): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }

  private prepareChannelActivation(tenantSlug: string, email: string, password: string): void {
    this.activationState.set('preparing');
    this.activationMessage.set('Preparando tu acceso para conectarte con Telegram o WhatsApp...');

    const payload: LoginRequest = {
      email,
      password,
      tenantSlug
    };

    this.http.post<LoginResponse>(`${this.apiBaseUrl}/api/auth/login`, payload).subscribe({
      next: (response) => {
        this.authSession.set(response);
        this.activationState.set('ready');
        this.activationMessage.set('Tu cuenta ya quedo lista. Elige donde deseas iniciar.');
      },
      error: () => {
        this.activationState.set('error');
        this.activationMessage.set('La cuenta se creo, pero no pudimos preparar el acceso automatico al bot.');
      }
    });
  }

  private buildChannelUrl(channel: ChannelType, linkCode: string): string {
    if (channel === 'TELEGRAM') {
      return `https://t.me/${this.telegramBotUsername}?start=${encodeURIComponent(linkCode)}`;
    }

    return `https://wa.me/${this.whatsappBotNumber}?text=${encodeURIComponent(`vincular ${linkCode}`)}`;
  }

  private openChannel(url: string): void {
    window.open(url, '_blank', 'noopener,noreferrer');
  }
}
