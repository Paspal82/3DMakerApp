Questo file spiega in che modo Visual Studio ha creato il progetto.

Per generare questo progetto sono stati usati gli strumenti seguenti:
- Angular CLI (ng)

Per generare questo progetto sono stati eseguiti i passaggi seguenti:
- Creare un progetto Angular con ng: `ng new 3dmakerapp.client --defaults --skip-install --skip-git --no-standalone `.
- Aggiungere `proxy.conf.js` a chiamate proxy al server ASP.NET back-end.
- Aggiungere uno script `aspnetcore-https.js` per installare i certificati https.
- Aggiornare `package.json` per chiamare `aspnetcore-https.js` e servire con https.
- Aggiornare `angular.json` in modo che punti a `proxy.conf.js`.
- Aggiornare il componente app.component.ts per recuperare e visualizzare le informazioni meteo.
- Modificare app.component.spec.ts con test aggiornati.
- Aggiornare app.module.ts per importare HttpClientModule.
- Creare il file di progetto (`3dmakerapp.client.esproj`).
- Creare `launch.json` per abilitare il debug.
- Aggiornare package.json per aggiungere `jest-editor-support`.
- Aggiornare package.json per aggiungere `run-script-os`.
- Aggiungere `karma.conf.js` per i test di unità.
- Aggiornare `angular.json` in modo che punti a `karma.conf.js`.
- Aggiungi progetto alla soluzione.
- Aggiornare l'endpoint proxy come endpoint del server back-end.
- Aggiungere il progetto all'elenco dei progetti di avvio.
- Scrivere questo file.
