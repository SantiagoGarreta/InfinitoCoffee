# Sucursales

El sistema utiliza una sola base de datos para **Sucursal 1** y **Sucursal 2**. Los ingredientes, artículos, catálogo, precios y recetas son comunes. Cada local tiene sus propios productos terminados, pedidos, numeración del 1 al 99, ventas, resultados, cocina y pantalla de pickup.

## Activación

Aplicar las migraciones antes de iniciar la nueva versión:

```powershell
dotnet run --project .\src\InfinitoCoffee.DbSetup -- migrate
```

La migración `20261004232238_AddBranches` asigna usuarios, pedidos, ventas y productos terminados existentes a **Sucursal 1**. Los ingredientes conservan un único saldo compartido. **Sucursal 2** comienza sin pedidos ni productos terminados. El historial conserva los movimientos anteriores.

## Usuarios y operación

- Administración tiene el selector **Sucursal activa** en la barra lateral de todas las pantallas privadas (Caja, Cocina, Stock, Resultado y demás secciones). El cambio recarga la pantalla para cargar los pedidos, existencias y conexiones del local elegido. Guardar cualquier formulario pendiente antes de cambiar. Las cuentas de caja y cocina muestran su sucursal asignada y no pueden cambiarla desde la sesión.
- **Cambiar nombre** permite renombrar la sucursal seleccionada. Los nombres se usan en las pantallas y el historial; su identificador permanece igual.
- En **Administración → Usuarios**, asignar la sucursal de cada cuenta de caja o cocina. Estas cuentas solo pueden operar en el local asignado; la API también controla el acceso.
- Si se cambia la sucursal de una cuenta de caja o cocina con sesión abierta, debe volver a iniciar sesión. Sus siguientes solicitudes operativas requieren renovar la sesión.
- **Resultado** calcula ingresos, costos, ganancias, productos vendidos y estadísticas de la sucursal seleccionada.
- Los eventos de cocina y pickup se publican únicamente al canal de la sucursal del pedido. Dos locales pueden tener simultáneamente una comanda con el mismo número.

## Pantallas de pickup

El enlace **Pickup** abre la pantalla del local actual en otra pestaña. Para configurar cada televisor, usar una URL fija:

- Sucursal 1: `/pickup?branchId=1`
- Sucursal 2: `/pickup?branchId=2`

Pickup sigue siendo público y tiene su propio selector. Su sucursal se toma de la URL, independientemente de cualquier sesión administrativa abierta en el navegador. `/pickup` sin parámetro muestra Sucursal 1. Un identificador explícito inválido produce un error.

## Stock

Producir en cualquier sucursal descuenta del mismo saldo de ingredientes y suma productos únicamente al local seleccionado. Las ventas descuentan sus productos terminados. Para movimientos manuales entre locales, ver la [guía de stock](stock.md).

## API

Las operaciones privadas de `/api/orders` y `/api/stock` usan la cabecera `X-Branch-Id`. Si se omite, se usa la sucursal asignada al usuario. Un usuario de caja o cocina no puede indicar otra sucursal. Administración puede seleccionar cualquiera de las dos.

Pickup y las conexiones SignalR aceptan `branchId` en la URL. `/api/branches` lista identificadores y nombres públicos; `PUT /api/branches/{id}` permite renombrar una sucursal como administrador. Los formularios de usuarios admiten `branchId`.

El saldo y los movimientos de ingredientes utilizan `branchId: 0` para indicar que son compartidos. Las operaciones identifican el local de origen con 1 o 2. Los aumentos de productos mediante `/api/stock/adjustments` consumen ingredientes por defecto; enviar `consumeIngredients: false` para ingresos sin producción. El identificador de operación es idempotente y no puede reutilizarse desde otra sucursal.
