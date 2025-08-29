# Verbesserte Lobby-Discovery Testanleitung

## Problembehebung für VPN-Netzwerke

Die ursprüngliche Discovery hatte Probleme mit VPN-Verbindungen und mehreren Netzwerk-Interfaces. Die neue Implementierung behebt diese Probleme:

### Was wurde verbessert:

1. **Multi-Interface Broadcasting**: Sendet Discovery-Nachrichten an alle Netzwerk-Segmente
2. **VPN-Kompatibilität**: Bessere Behandlung von VPN-Adressen wie 100.x.x.x und 25.x.x.x
3. **Intelligente IP-Auswahl**: Wählt die beste Host-IP basierend auf dem Client-Netzwerk
4. **Erhöhte Timeouts**: 8 Sekunden Discovery-Zeit für VPN-Latenzen

### Testen der neuen Version:

#### Host starten:
```bash
dotnet run
# Namen eingeben: "TestHost"
# Modus wählen: "host" oder "h"
```

Die neue Version zeigt:
- Netzwerk-Diagnose beim Start
- Alle verfügbaren IP-Adressen mit Prioritäts-Kennzeichnung:
  - (LAN - beste Wahl) für 192.168.x.x, 10.x.x.x, 172.x.x.x
  - (VPN - könnte funktionieren) für 100.x.x.x, 25.x.x.x
- Discovery-Port 5001 und Game-Port 5000 Information

#### Client starten:
```bash
dotnet run
# Namen eingeben: "TestClient"  
# Modus wählen: "client" oder "c"
```

Die neue Version:
- Führt automatisch Netzwerk-Diagnose durch
- Zeigt alle gefundenen Lobbys mit IP-Adressen
- Bietet hilfreiche Fehlermeldungen wenn keine Lobbys gefunden werden
- Hat erhöhten Timeout für VPN-Umgebungen

### Bei Problemen:

1. **Firewall prüfen**: UDP-Port 5001 muss offen sein
2. **VPN-Einstellungen**: Manche VPNs blockieren Broadcast-Traffic
3. **Log-Dateien prüfen**: 
   - `tetris_multiplayer_network.log` - Netzwerk-Details
   - `tetris_multiplayer.log` - Allgemeine Logs

### Erwartetes Verhalten:

- Host zeigt alle seine IP-Adressen mit Netzwerk-Typ
- Client findet Hosts auch über VPN-Verbindungen
- Bessere Fehlerbehandlung und Diagnose-Informationen
- Fallback zu manueller IP-Eingabe funktioniert weiterhin