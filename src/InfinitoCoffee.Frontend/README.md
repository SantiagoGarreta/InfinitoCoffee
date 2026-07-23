# InfinitoCoffee.Frontend

Frontend Angular 22 con SSR y una base mÃ­nima de tiempo real para cocina y pickup.

## Rutas

- `/kitchen`
- `/pickup`

## Tiempo real

- Hub SignalR: `http://localhost:5165/hubs/orders`
- Eventos: `OrderCreated`, `OrderStatusChanged`, `OrderCancelled`
- SincronizaciÃ³n inicial: REST
- ResincronizaciÃ³n despuÃ©s de reconectar: REST

## Desarrollo

```bash
npm install
npm start
```

## Tests

```bash
npm run test -- --run
```

## Build

```bash
npm run build
```

La app evita iniciar SignalR durante SSR. La conexiÃ³n se crea solo en navegador.
