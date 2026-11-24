# Sistema de Finanzas Personales (API REST — .NET 10 & PostgreSQL)

API REST multiusuario para la gestión de finanzas personales, desarrollada con **ASP.NET Core (.NET 10)**, **Entity Framework Core (Npgsql)** y **PostgreSQL** mediante enfoque *Code First* y migraciones[cite: 1, 2, 3].

## Flujo de colaboración y reglas del repositorio

* **Repositorio público base:** Administrado por el líder del proyecto. **Ningún integrante debe ser agregado como colaborador directo** en el repositorio base.
* **Rama de integración:** Los cambios y aportes se integran en la rama **`develop`** del repositorio base.
* **Trabajo por Fork y Pull Request:** Cada integrante realiza un *fork* del repositorio base, desarrolla en una rama de trabajo en su *fork* y envía sus cambios mediante **Pull Request (PR)** hacia `develop` en el repositorio base.
* **Revisión e integración:**
  * En las PR de los integrantes, el líder revisa, aprueba e integra en `develop`.
  * En las PR creadas por el propio líder, otro integrante registra su revisión y el líder realiza la integración en `develop`.

## Reparto de tablas (12 entidades — 4 por integrante)

Cada entidad tiene un único responsable para evitar colisiones y duplicidad de trabajo.

| Integrante (Rol / Usuario GitHub) | Tablas asignadas (4 por persona) | Alcance funcional |
| :--- | :--- | :--- |
| **Integrante 1** (`@usuario-dev1`) | `Usuario`, `Cuenta`, `Categoria`, `Movimiento`[cite: 1] | Autenticación/usuarios, cuentas (efectivo, banco, billetera digital), categorías y registro de ingresos/gastos[cite: 2, 4, 5]. |
| **Integrante 2** (`@usuario-dev2`) | `Transferencia`, `PreferenciaUsuario`, `Etiqueta`, `MovimientoEtiqueta` | Transferencias entre cuentas propias, preferencias de usuario y etiquetado transversal de movimientos. |
| **Integrante 3** (`@usuario-dev3`) | `PresupuestoMensual`, `MetaAhorro`, `MovimientoRecurrente`, `RecordatorioPago` | Límites mensuales por período, metas de ahorro, plantillas recurrentes y recordatorios de vencimiento. |

> **Nota:** Cada aporte de tablas debe incluir sus clases de modelo (`Models/`), configuración en `DbContext` / Fluent API (`Data/Configurations/`) y su migración de EF Core (`Migrations/`) o script SQL correspondiente[cite: 3, 9, 10].

## Modelo de datos — 12 tablas y sus campos

El sistema se organiza en 12 tablas con aislamiento por usuario (`usuario_id`), identificadores `uuid` (`Guid`), montos decimales (`numeric(14,2)` / `decimal`) y fechas en `date` (`DateOnly`) y `timestamptz` (`DateTimeOffset`)[cite: 3, 8, 9].

### Bloque 1: Integrante 1 (Líder) — Núcleo financiero y catálogos

* **`Usuario`** — Representa a cada persona registrada y aísla sus datos en el sistema[cite: 2, 4].
  * **Campos:** `id` (`uuid`, PK), `nombre` (`varchar(100)`), `email` (`varchar(150)`, único sin distinguir mayúsculas), `password_hash` (`varchar(255)`), `fecha_creacion` (`timestamptz`), `activo` (`boolean`, por defecto `true`)[cite: 3, 4].
  * **Impacto:** Filtra toda consulta y escritura por usuario autenticado; un usuario inactivo no puede iniciar sesión ni operar[cite: 2, 4].
* **`Cuenta`** — Almacena los depósitos de dinero de cada persona (efectivo, banco, billetera digital)[cite: 2, 4].
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK → `usuario.id`), `nombre` (`varchar(100)`), `tipo` (`varchar(30)`: `efectivo`, `banco`, `billetera_digital`), `moneda` (`varchar(3)`, por defecto `'BOB'`), `saldo_inicial` (`numeric(14,2)`, $\ge 0$), `activa` (`boolean`, por defecto `true`)[cite: 3, 4].
  * **Impacto:** Su saldo se calcula a partir de `saldo_inicial` más ingresos, menos gastos y transferencias; si se archiva (`activa = false`), conserva su historial y no recibe nuevos movimientos[cite: 4, 6].
* **`Categoria`** — Clasifica los ingresos y gastos de cada usuario[cite: 4, 5].
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK → `usuario.id`), `nombre` (`varchar(100)`), `tipo` (`varchar(10)`: `ingreso` o `gasto`), `limite_mensual` (`numeric(14,2)`, opcional, $\ge 0$ solo en gasto), `activa` (`boolean`, por defecto `true`)[cite: 3, 4].
  * **Impacto:** Evita nombres duplicados entre categorías activas del mismo usuario y tipo, y exige que el movimiento coincida en propietario y tipo (`ingreso` o `gasto`)[cite: 5].
* **`Movimiento`** — Registra cada ingreso o gasto real (incluyendo sueldos ingresados manualmente)[cite: 2, 5, 7].
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK), `cuenta_id` (`uuid`, FK), `categoria_id` (`uuid`, FK), `tipo` (`varchar(10)`: `ingreso` o `gasto`), `monto` (`numeric(14,2)`, $> 0$), `concepto` (`varchar(200)`), `fecha_movimiento` (`date`), `notas` (`text`, opcional), `fecha_registro` (`timestamptz`)[cite: 3, 5].
  * **Impacto:** Alimenta el historial, los totales mensuales, los gráficos por categoría y el cálculo del saldo de cada cuenta[cite: 6, 7, 8].

### Bloque 2: Integrante 2 — Transferencias, preferencias y etiquetas

* **`Transferencia`** — Registra el envío de dinero entre dos cuentas propias del mismo usuario.
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK), `cuenta_origen_id` (`uuid`, FK → `cuenta.id`), `cuenta_destino_id` (`uuid`, FK → `cuenta.id`), `monto` (`numeric(14,2)`, $> 0$), `comision` (`numeric(14,2)`, $\ge 0$, por defecto `0`), `fecha_transferencia` (`date`), `concepto` (`varchar(200)`), `fecha_registro` (`timestamptz`).
  * **Impacto:** Ajusta los saldos de la cuenta origen (`-monto - comision`) y destino (`+monto`) sin crear filas en `Movimiento` para no inflar ingresos ni gastos.
* **`PreferenciaUsuario`** — Guarda configuraciones personales y de interfaz separadas de `Usuario`.
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK única → `usuario.id`), `moneda_principal` (`varchar(3)`, por defecto `'BOB'`), `dia_inicio_mes` (`smallint`, `1` a `28`, por defecto `1`), `alerta_limite_porcentaje` (`smallint`, `1` a `100`, por defecto `80`), `tema_ui` (`varchar(20)`, por defecto `'claro'`).
  * **Impacto:** Define el ciclo mensual de consulta y el umbral de alerta de límites sin modificar el saldo de las cuentas.
* **`Etiqueta`** — Define etiquetas transversales por usuario para agrupar gastos o ingresos (por ejemplo, *Viaje* o *Reembolsable*).
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK → `usuario.id`), `nombre` (`varchar(50)`), `color_hex` (`varchar(7)`), `activa` (`boolean`, por defecto `true`).
  * **Impacto:** Permite filtrar y agrupar movimientos de distintas categorías bajo un mismo tema sin alterar los saldos.
* **`MovimientoEtiqueta`** — Tabla intermedia (`N:M`) entre `Movimiento` y `Etiqueta`.
  * **Campos:** `movimiento_id` (`uuid`, PK, FK → `movimiento.id`), `etiqueta_id` (`uuid`, PK, FK → `etiqueta.id`).
  * **Impacto:** Vincula uno o varios tags a un movimiento; al eliminar un movimiento, se limpia su relación sin afectar el saldo de la cuenta.

### Bloque 3: Integrante 3 — Planificación, metas y recurrencia

* **`PresupuestoMensual`** — Registra límites de gasto por categoría para un año y mes específicos.
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK), `categoria_id` (`uuid`, FK → `categoria.id`), `anio` (`smallint`), `mes` (`smallint`, `1` a `12`), `monto_limite` (`numeric(14,2)`, $\ge 0$).
  * **Impacto:** Permite mantener límites históricos o puntuales por mes (complementando `limite_mensual` de `Categoria`[cite: 4, 7]) sin alterar saldos reales.
* **`MetaAhorro`** — Representa objetivos de ahorro personales con avance registrado.
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK), `cuenta_vinculada_id` (`uuid`, opcional, FK → `cuenta.id`), `nombre` (`varchar(100)`), `monto_objetivo` (`numeric(14,2)`, $> 0$), `monto_ahorrado` (`numeric(14,2)`, $\ge 0$, por defecto `0`), `fecha_limite` (`date`, opcional), `estado` (`varchar(20)`: `activa`, `completada`, `pausada`).
  * **Impacto:** Muestra el progreso hacia metas de ahorro en el panel sin bloquear ni duplicar el saldo real de `Cuenta`.
* **`MovimientoRecurrente`** — Almacena plantillas de ingresos o gastos periódicos (sueldo, alquiler, suscripciones)[cite: 2].
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK), `cuenta_id` (`uuid`, FK), `categoria_id` (`uuid`, FK), `tipo` (`varchar(10)`: `ingreso` o `gasto`), `monto_estimado` (`numeric(14,2)`, $> 0$), `concepto` (`varchar(200)`), `frecuencia` (`varchar(20)`: `semanal`, `quincenal`, `mensual`, `anual`), `dia_ejecucion` (`smallint`), `proxima_fecha` (`date`), `activo` (`boolean`, por defecto `true`).
  * **Impacto:** Sirve como plantilla para proyectar pagos/ingresos y generar un `Movimiento` confirmado cuando corresponda, avanzando `proxima_fecha`.
* **`RecordatorioPago`** — Agenda vencimientos de pagos puntuales o asociados a una recurrencia.
  * **Campos:** `id` (`uuid`, PK), `usuario_id` (`uuid`, FK), `movimiento_recurrente_id` (`uuid`, opcional, FK → `movimiento_recurrente.id`), `titulo` (`varchar(120)`), `monto_estimado` (`numeric(14,2)`, opcional, $\ge 0$), `fecha_vencimiento` (`date`), `pagado` (`boolean`, por defecto `false`), `fecha_pago` (`date`, opcional), `notas` (`text`, opcional).
  * **Impacto:** Muestra avisos de vencimientos próximos o pendientes sin modificar por sí solo el saldo de ninguna cuenta.

## Reparto de inicio de sesión externo (2 proveedores por integrante)

Registrado **antes de programar**. Cada integrante implementa **2 botones funcionales**: **1 de lista fácil** y **1 de lista media o difícil**, sin repetir proveedores entre integrantes del equipo.

* **Lista fácil:** Discord, Twitch, Dropbox.
* **Lista media:** Google, Microsoft, Facebook, Slack, Spotify.
* **Lista difícil:** Apple, X, Meta Advanced Access.

| Integrante | Proveedor fácil | Proveedor medio o difícil |
| :--- | :--- | :--- |
| **Integrante 1** (`@usuario-dev1`) | Discord | Google |
| **Integrante 2** (`@usuario-dev2`) | Twitch | Spotify |
| **Integrante 3** (`@usuario-dev3`) | Dropbox | Microsoft |

> Las credenciales de cada proveedor y las URL de retorno se configuran mediante **variables de entorno** (no se escriben credenciales en el código ni se suben al repositorio)[cite: 10].

## Requisitos y ejecución local

1. Tener instalado **.NET 10 SDK** y **PostgreSQL**[cite: 2, 3].
2. Configurar la cadena de conexión a PostgreSQL y las variables de entorno de autenticación mediante variables de entorno o configuración local no versionada (sin subir credenciales sensibles)[cite: 9, 10].
3. Restaurar dependencias y aplicar migraciones[cite: 9, 10]:
   ```bash
   dotnet restore
   dotnet ef database update --project src/FinanzasPersonales.Api
   ```
4. Ejecutar la API:
   ```bash
   dotnet run --project src/FinanzasPersonales.Api
   ```

## Autenticación externa unificada — seis proveedores

Google, Discord, Twitch, Spotify, Dropbox y Microsoft usan la misma estructura: cada controlador declara únicamente su proveedor, esquema y rutas, y hereda login/consulta de identidad de `ExternalAuthControllerBase`. Toda la configuración está en `Authentication/ExternalAuthentication.cs`, con **un único registro de `Cookies`**. Esto resuelve el error de arranque `Scheme already exists: Cookies`.

La única página es **https://localhost:7133/**, con el diseño oscuro, los seis botones, avisos de configuración/errores y la identidad activa. `/auth/login` y `/auth/dev3` redirigen a ella; ya no existe una segunda vista Razor. Después del login, cancelación o logout se vuelve a la misma interfaz. El puerto 7133 procede del perfil `https` existente en `Properties/launchSettings.json`.

| Operación | Ruta |
| --- | --- |
| Página, botones, avisos e identidad | `GET /` |
| Login de cada proveedor | `GET` o `POST /api/auth/{proveedor}/login` |
| Identidad del proveedor activo (401 sin sesión, 403 si es otro) | `GET /api/auth/{proveedor}/user` |
| Identidad compartida | `GET /api/auth/me` |
| Token CSRF para clientes del mismo origen | `GET /api/auth/csrf` |
| Estado público de configuración, sin credenciales | `GET /api/auth/providers` |
| Cerrar sesión local | `POST /api/auth/logout` |
| Alias anteriores Dev3 | `/auth/dev3`, `/auth/dev3/session`, `/auth/dev3/logout`, `/auth/dropbox/login`, `/auth/microsoft/login` |

`{proveedor}` es `google`, `discord`, `twitch`, `spotify`, `dropbox` o `microsoft`. Los POST de login y logout exigen token antiforgery (formulario o header `RequestVerificationToken`). GET login conserva los enlaces existentes, pero siempre usa state/correlación del middleware. `returnUrl` solo permite rutas locales: se rechazan redirecciones externas.

Los callbacks los atienden los middleware oficiales, no acciones MVC manuales. Registrar estas URL exactas para el perfil HTTPS actual:

| Proveedor | Callback completo | Protocolo |
| --- | --- | --- |
| Google | `https://localhost:7133/api/auth/google/callback` | OIDC, Code + PKCE, `form_post` |
| Discord | `https://localhost:7133/api/auth/discord/callback` | OAuth Code confidencial |
| Twitch | `https://localhost:7133/signin-twitch` | OAuth Code confidencial |
| Spotify | `https://localhost:7133/signin-spotify` | OAuth Code + PKCE |
| Dropbox | `https://localhost:7133/auth/dropbox/callback` | OAuth Code + PKCE |
| Microsoft | `https://localhost:7133/auth/microsoft/callback` | OIDC, Code + PKCE, `form_post` |

Cada proveedor mantiene las particularidades de su protocolo, no una validación criptográfica manual. No usar los callbacks como botones de login. Si antes Google/Discord tenían registrado `http://localhost:5257/...`, actualizar sus portales para el origen HTTPS real. Se acepta el antiguo `RedirectUri` para extraer **la ruta**, pero el origen/puerto del callback se obtiene de la petición actual.

### Configuración local sin secretos versionados

1. Copiar `.env.example` a `.env` y completar las credenciales **solo en ese archivo local**. Las credenciales vacías de la plantilla son intencionales: no son aplicaciones OAuth ficticias utilizables. Entrecomillar los valores de secretos si contienen caracteres de shell.
2. Configurar estas variables:

   | Variable | Valor |
   | --- | --- |
   | `Authentication__Dropbox__ClientId` | App key de Dropbox |
   | `Authentication__Dropbox__ClientSecret` | App secret de Dropbox |
   | `Authentication__Dropbox__CallbackPath` | `/auth/dropbox/callback` |
   | `Authentication__Microsoft__ClientId` | Application (client) ID |
   | `Authentication__Microsoft__ClientSecret` | **Valor** del secreto, no Secret ID |
   | `Authentication__Microsoft__TenantId` | `common` para personales y organizaciones; `consumers` solo personales; `organizations` solo organizaciones; o GUID de tenant |
   | `Authentication__Microsoft__CallbackPath` | `/auth/microsoft/callback` |

   Google, Discord, Twitch y Spotify usan exactamente `Authentication__{Proveedor}__ClientId`, `__ClientSecret` y `__CallbackPath`, con las rutas de la tabla anterior. Las antiguas claves `Auth__Twitch__...`/`AUTH__TWITCH__...` y Spotify siguen siendo compatibles; preferir el prefijo uniforme `Authentication`. Los valores `tu_...`, `placeholder_...` y vacíos no activan un proveedor.

3. El proyecto usa `Env.NoClobber().TraversePath().Load()`: encuentra `.env` también al ejecutar desde `src/` y respeta variables ya exportadas. Desde la raíz, en Bash, también se puede cargar explícitamente el archivo propio y ejecutar:

   ```bash
   # Solo la primera vez, si aún no existe tu archivo local:
   cp .env.example .env
   chmod 600 .env
   # Editar .env antes de continuar. No sobrescribirlo con la plantilla después.
   set -a
   source .env
   set +a

   dotnet dev-certs https --trust
   dotnet restore src/FinanzasPersonales.Api
   dotnet build src/FinanzasPersonales.Api
   dotnet run --project src/FinanzasPersonales.Api --launch-profile https
   ```

   Si Linux solicita pasos adicionales para confiar el certificado en el navegador, seguir las instrucciones de `dotnet dev-certs`. No desactivar la validación TLS de los proveedores. No hace falta aplicar migraciones para probar estos botones.

Sin ClientId y ClientSecret el proveedor no se registra: la página lo indica y el botón muestra un aviso controlado. Reiniciar la API al cambiar las variables. Los callbacks personalizados deben ser rutas locales distintas entre sí y no ocupar login, user, logout, identidad o CSRF; actualizar también los portales. No se necesita una conexión PostgreSQL para crear una sesión externa.

### Registrar Dropbox

1. Entrar en [Dropbox App Console](https://www.dropbox.com/developers/apps) y crear una aplicación con **Scoped access**. Elegir **App folder** si se solicita acceso a contenido: este login no necesita permisos de archivos.
2. En **Permissions**, habilitar únicamente `account_info.read` y guardar los cambios.
3. En **Settings → OAuth 2 → Redirect URIs**, agregar exactamente:
   **`https://localhost:7133/auth/dropbox/callback`**.
4. Se usa Authorization Code con PKCE S256. Si el panel presenta el ajuste **Allow public clients** que habilita PKCE, activarlo. El backend sigue siendo confidencial y conserva el App secret solo en el servidor. No seleccionar un flujo implícito en la aplicación.
5. Copiar **App key** como ClientId y **App secret** como ClientSecret al entorno local. No usar el botón de generar un token como sustituto del login.
6. Si la aplicación está en modo desarrollo, habilitar los usuarios de prueba que permita el panel antes de probar con compañeros.

El middleware intercambia el código por HTTPS y consulta `POST https://api.dropboxapi.com/2/users/get_current_account` con el token en `Authorization: Bearer`. Solo se pide identidad; no acceso offline ni refresh tokens.

### Registrar Microsoft

1. Entrar en [Microsoft Entra admin center](https://entra.microsoft.com/) → **App registrations → New registration**. Es necesario tener acceso a un directorio con permiso para registrar aplicaciones; si la cuenta no lo permite, solicitarlo al administrador.
2. Para `TenantId=common`, elegir **Accounts in any organizational directory and personal Microsoft accounts**. Para `consumers`, elegir el público de cuentas personales. El público del registro y TenantId deben coincidir.
3. Agregar plataforma **Web**, no SPA, y registrar exactamente:
   **`https://localhost:7133/auth/microsoft/callback`**.
4. Copiar **Application (client) ID**. En **Certificates & secrets**, crear un secreto y copiar su **Value** inmediatamente; se muestra una sola vez. Guardarlo solo en el entorno y controlar su caducidad.
5. No habilitar implicit grant ni public client flows: se usa **Authorization Code + PKCE** desde el backend. Se solicitan únicamente `openid profile email`; no acceso offline ni permisos a archivos de Graph.
6. Los tenants institucionales pueden requerir consentimiento administrativo conforme a sus políticas.

El middleware OIDC valida state/correlación, nonce, firma, audiencia y vigencia. El emisor se valida con `AadIssuerValidator`, biblioteca oficial de Microsoft compatible con emisores por tenant. No se desactiva la validación de emisor para aceptar cuentas personales.

### Sesión y límites de seguridad

- Una sola cookie `.FinanzasPersonales.Auth`, Secure, HttpOnly, SameSite=Lax y duración absoluta de 30 minutos, sin persistencia ni renovación deslizante. Los seis proveedores usan esa misma sesión.
- Correlación y nonce con cookies Secure/HttpOnly; los POST locales tienen protección antiforgery.
- No se guardan tokens en la cookie (`SaveTokens=false`) ni se devuelven al navegador. Los diagnósticos internos de los handlers OAuth/OIDC se silencian para evitar registrar respuestas remotas; los fallos registran únicamente un aviso sanitizado.
- Errores y cancelaciones muestran mensajes de una lista cerrada en la página oscura. Los textos se insertan mediante `textContent`, nunca como HTML. Las consultas de identidad/configuración evitan caché; al volver con Atrás se actualiza la sesión. Si falla otro proveedor, se avisa que la sesión anterior sigue activa.
- La identidad estable es proveedor + ID para OAuth, y emisor + sub verificados para Google/Microsoft. Los seis emiten los mismos claims (`NameIdentifier`, `Name`, email opcional, `provider` y `external_provider`). El correo es solo informativo.
- Ya existe `Usuario`, pero esta rama no tiene una vinculación segura para los seis proveedores. **No se crean ni vinculan usuarios locales por correo** ni se inventan emails o Guid. `/api/auth/me` devuelve `usuarioId: null` mientras no exista un vínculo local confiable; `identificadorExterno` es un dato diferente y nunca debe usarse como Guid financiero. Los servicios anteriores de login manual se conservan en disco, sin registro ni rutas activas; los controladores nuevos no los llaman. La autorización financiera requiere una integración explícita posterior.
- Cerrar sesión elimina la cookie local; no revoca autorizaciones ni cierra la sesión del proveedor. Los CRUD existentes conservan su comportamiento: este aporte no implementa su autorización.
- `Program.cs` conserva el contexto, los servicios CRUD, Scalar, sesión ASP.NET y archivos estáticos. Se retiró el OAuth inline/registro duplicado de cookie y se usa una sola extensión de autenticación. No se modifican modelos, tablas ni migraciones.

### Prueba manual y evidencias reales

Comprobaciones automatizadas sin credenciales reales (utilizan sus propios puertos libres):

```bash
dotnet restore src/FinanzasPersonales.Api
dotnet build src/FinanzasPersonales.Api
python3 tests/verify_auth.py
python3 tests/verify_auth_sessions.py
```

El primer script inicia/detiene su propia API en un puerto libre y comprueba los seis controladores, página, sesión anónima, 401, CSRF, redirecciones abiertas, callbacks alterados/reutilizados, cancelación y redirecciones oficiales con IDs deliberadamente ficticios. No canjea códigos ni autentica usuarios. Google/Microsoft requieren acceso a su metadata OIDC pública. Solo su cliente local omite la confianza del certificado de desarrollo; no altera TLS del backend a los proveedores. El comando anterior `python3 tests/verify_dev3_auth.py` ejecuta ahora esta misma batería común.

`verify_auth_sessions.py` compila un harness contra la API existente, sin otro proyecto .NET. Usa perfiles simulados, claves de cookie efímeras y ninguna conexión PostgreSQL. Comprueba el contrato normalizado de los seis proveedores, cookies autenticadas, `/me`, `/user`, 403 al consultar otro proveedor, CSRF, logout y 401 después de salir. Para OIDC simula el resultado posterior a la validación: no demuestra la aceptación de tokens reales de Google/Microsoft. No crea rutas de pruebas ni mecanismos de bypass en producción.

Para **cada uno de los seis proveedores**, con credenciales válidas y la API ejecutándose mediante HTTPS:

1. Abrir `/` y pulsar su botón.
2. Confirmar que se navega al dominio oficial de ese proveedor y autenticarse/consentir.
3. Regresar automáticamente a `/` y comprobar proveedor, identificador y nombre en la tarjeta oscura; correo cuando esté disponible.
4. Abrir `/api/auth/me` en el mismo navegador: debe indicar `isAuthenticated: true`, y `/api/auth/{proveedor}/user` debe responder 200.
5. Volver a la página, pulsar **Cerrar sesión** y recargar `/api/auth/me`: debe indicar `isAuthenticated: false`; el endpoint user vuelve a 401.
6. Repetir cancelando el consentimiento. Probar también el callback sin state o con un state alterado: no debe crearse sesión.

Capturar la compilación, botones, dominio oficial, identidad de retorno y sesión inactiva después de salir. **No capturar secretos, cookies, tokens, códigos de autorización ni URLs completas con state/code**. El login real solo se considera verificado tras completar cada ciclo en navegador con las aplicaciones registradas; compilar o comprobar redirecciones no demuestra autenticación real.

## Controladores Dev3 — Punto 3

### Doce endpoints protegidos

Cada recurso tiene `POST /api/{recurso}`, `GET /api/{recurso}` y `GET /api/{recurso}/{id}`:

- `presupuestos-mensuales`
- `metas-ahorro`
- `movimientos-recurrentes`
- `recordatorios-pago`

Todos usan `[Authorize]` con la cookie existente. Sin sesión devuelven **401**, no una redirección. Se filtra siempre por el usuario local; un ID inexistente o ajeno devuelve **404**. Los DTO de creación no admiten `UsuarioId` ni otros campos desconocidos. Las lecturas usan `AsNoTracking`, las operaciones son asíncronas y aceptan cancelación.

Los POST responden **201 + Location**, errores de datos/relaciones **400** y presupuestos duplicados **409** (incluido el índice único para solicitudes concurrentes). Los importes deben caber en `numeric(14,2)` sin redondeo silencioso. Se conserva `Año`/`año`, nombre existente del presupuesto, con rango 1–9999; mes 1–12. Las recurrencias semanales usan 1–7 y las demás 1–28. Las fechas obligatorias no pueden omitirse ni ser `0001-01-01`; se permiten fechas históricas. No se generan movimientos ni se alteran saldos.

### Vincular las identidades reales sin modificar tablas ni OAuth

Dropbox/Microsoft emiten una identidad externa, **no un Guid local**. El servicio OAuth integrado de otros proveedores usa correo para vincular; Dev3 no lo reutiliza ni modifica. Para estos cuatro controladores se utiliza una lista explícita administrada en el servidor:

1. Completar un login real de Dropbox en `/auth/dev3`.
2. Consultar `/auth/dev3/session` y obtener `provider` e `id`. Ese `id` incluye el identificador estable del proveedor, no un correo.
3. Un administrador/responsable debe verificar la pertenencia y seleccionar un `Usuario.Id` **real, existente y activo**, sin deducirlo solo del correo.
4. Configurar en el entorno local no versionado:

   ```text
   Dev3__VinculosIdentidad__0__Proveedor=Dropbox
   Dev3__VinculosIdentidad__0__IdentificadorExterno='IDENTIFICADOR_REAL_DE_LA_SESION'
   Dev3__VinculosIdentidad__0__UsuarioId=GUID_LOCAL_REAL
   ```

5. Repetir para Microsoft con índice `1`, proveedor `Microsoft`, `id` completo de su sesión (`emisor|sub`) y su Guid local verificado. Solo si ambas identidades pertenecen realmente a la misma persona pueden apuntar al mismo usuario local.
6. Exportar las variables y reiniciar la API. No enviar esta configuración en solicitudes HTTP. `.env.example` incluye las claves comentadas, sin usuarios fijos ni ficticios.

El filtro `AccesoDev3Filter` coteja el proveedor y el ID de la cookie autenticada con la configuración del servidor, verifica en PostgreSQL que el usuario siga activo y obtiene de allí el propietario para las cuatro acciones. Ignora headers o claims arbitrarios como `usuario_id` para vincular estos proveedores.

Si falta el vínculo, la sesión está autenticada pero no tiene autorización financiera: **403**. Usuario local inactivo/inexistente: **403**. Base no disponible o sin esquema: **503**, sin devolver detalles de conexión. No existe un endpoint público para autoasignarse usuarios locales. Una sesión de otro proveedor tampoco obtiene acceso por coincidencia de correo.

### PostgreSQL y migraciones

No se añaden entidades, contextos, migraciones ni tablas al esquema de la aplicación en esta etapa. Esta rama no contiene migraciones/snapshot previos versionados. El responsable de integración debe proporcionar la base migrada con las entidades existentes antes de la prueba real; no usar `EnsureCreated()` ni ejecutar una migración inicial que mezcle aportes sin revisión.

La prueba de la conexión versionada encontró un servidor local disponible, pero rechazó la contraseña. No se cambian esas credenciales ni la base de desarrollo. Configurar `ConnectionStrings__DefaultConnection` con valores locales válidos y verificar que las tablas ya existan.

Para permitir que EF construya su modelo, se corrigió en `RelacionesDev3` la selección/orden de argumentos de `HasOne`: la sobrecarga genérica de un solo string lo interpretaba como navegación CLR. Es una corrección del mapeo previsto, no un cambio de esquema de otros integrantes.

### Scalar, cookies y CSRF

Abrir `/scalar/v1` mediante el mismo origen HTTPS después del login. Las doce operaciones aparecen en `/openapi/v1.json`. Los POST usan la cookie compartida y además requieren el header **`RequestVerificationToken`**, mostrado como parámetro en Scalar.

Obtener un token después de iniciar sesión, desde la consola del navegador en el mismo origen:

```javascript
const pagina = await fetch('/auth/dev3', { credentials: 'same-origin' }).then(r => r.text());
const doc = new DOMParser().parseFromString(pagina, 'text/html');
const csrf = doc.querySelector('input[name="__RequestVerificationToken"]').value;
```

No publicar ese token ni la cookie. Copiar `csrf` al parámetro de header en Scalar, o realizar un POST desde la misma consola:

```javascript
const respuesta = await fetch('/api/metas-ahorro', {
  method: 'POST',
  credentials: 'same-origin',
  headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': csrf },
  body: JSON.stringify({ nombre: 'Ahorro personal', montoObjetivo: 500, montoAhorrado: 0 })
});
console.log(respuesta.status, await respuesta.json());
```

Ejemplos de cuerpos para los otros recursos (reemplazar IDs por registros propios existentes):

```json
{"categoriaId":"GUID_CATEGORIA_GASTO_PROPIA","año":2026,"mes":11,"montoLimite":500}
```

```json
{"cuentaId":"GUID_CUENTA_PROPIA","categoriaId":"GUID_CATEGORIA_GASTO_PROPIA","tipo":"gasto","montoEstimado":80,"concepto":"Suscripción","frecuencia":"mensual","diaEjecucion":20,"proximaFecha":"2026-12-20","activo":true}
```

```json
{"titulo":"Pago pendiente","montoEstimado":80,"fechaVencimiento":"2026-12-20"}
```

En metas, `cuentaVinculadaId` es opcional. En recordatorios, `movimientoRecurrenteId` es opcional y, cuando se indica, debe pertenecer al mismo usuario. El token CSRF debe obtenerse de nuevo al cambiar la identidad/sesión.

### Pruebas automatizadas y evidencias del Punto 3

```bash
dotnet restore src/FinanzasPersonales.Api
dotnet build src/FinanzasPersonales.Api
python3 tests/verify_dev3_controllers.py
```

La prueba requiere SDK .NET 10, certificado HTTPS de desarrollo y los binarios locales PostgreSQL (`initdb`, `pg_ctl`). Compila un harness contra el proyecto existente, **sin crear otro proyecto ni otro DbContext**. Arranca un PostgreSQL desechable en un directorio temporal con socket Unix privado y sin TCP; crea únicamente el esquema del modelo existente como fixture, ejecuta las pruebas y elimina sus propios datos. Este DDL de prueba **no es una migración ni inicialización de la base de la aplicación**. No lee `.env`, no usa claves de cookies reales ni modifica el servidor de desarrollo.

Las comprobaciones abarcan los cuatro recursos con dos cookies autenticadas de prueba (identidades Dropbox/Microsoft simuladas), 201/200, persistencia real PostgreSQL, 401 sin sesión, 403 sin vínculo o usuario inactivo, 404 bidireccional entre usuarios, validación de relaciones/campos/importes, rechazo de `UsuarioId` enviado, CSRF, duplicados 409, no alteración de saldos y doce operaciones en OpenAPI/Scalar. **No se completó login interactivo real con los proveedores en esta prueba.**

Para evidencias finales, con aplicaciones OAuth y base real configuradas, repetir en navegador/Scalar para **cada proveedor y cada recurso**: POST 201, GET lista 200, GET por ID 200; sin cookie, POST y ambos GET 401; con otra identidad/local usuario, GET del ID ajeno 404 y relación ajena en POST 400. Capturar también el duplicado 409 y la salida real de build. Comprobar las filas mediante un SELECT en PostgreSQL con una conexión local segura, sin capturar secretos o cookies.

## Documentación de contribución

Consulta la guía completa en [CONTRIBUTING.md](CONTRIBUTING.md).
