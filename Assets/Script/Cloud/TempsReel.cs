using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// Canal temps réel Supabase (protocole Phoenix sur WebSocket) : reçoit les changements
/// des tables écoutées. Les messages arrivent sur un fil de fond et attendent dans
/// `Recus` ; le propriétaire les lit sur le fil principal (Update, EditorApplication.update).
/// Étape 1 (essai technique) : seule la fenêtre CIPL › Essai cloud l'utilise.
public class TempsReel : IDisposable
{
    public const string Sujet = "realtime:cipl";
    static readonly TimeSpan Battement = TimeSpan.FromSeconds(25);   // le serveur coupe après ~60 s sans signe de vie

    public readonly ConcurrentQueue<string> Recus = new ConcurrentQueue<string>();
    public volatile string Etat = "arrêté";
    public string RefAbonnement { get; private set; }

    ClientWebSocket _ws;
    CancellationTokenSource _cts;
    readonly SemaphoreSlim _envoi = new SemaphoreSlim(1, 1);   // un seul envoi à la fois sur la socket
    int _ref;

    public async Task Demarrer(string projetUrl, string cle, string jeton, params string[] tables)
    {
        Arreter();
        _cts = new CancellationTokenSource();
        _ws = new ClientWebSocket();
        Etat = "connexion…";
        await _ws.ConnectAsync(new Uri(UrlWebSocket(projetUrl, cle)), _cts.Token);

        var ws = _ws; var ct = _cts.Token;
        _ = Task.Run(() => Boucle(ws, ct));
        RefAbonnement = Prochain();
        await Envoyer(MessageJoin(tables, jeton, RefAbonnement));
        Etat = "abonnement demandé";
        _ = Task.Run(() => Battre(ct));
    }

    public void Arreter()
    {
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _ws?.Abort(); _ws?.Dispose(); } catch (Exception) { /* socket déjà fermée */ }
        _ws = null;
        Etat = "arrêté";
    }

    public void Dispose() => Arreter();

    string Prochain() => Interlocked.Increment(ref _ref).ToString();

    async Task Envoyer(string json)
    {
        var ws = _ws;
        if (ws == null || ws.State != WebSocketState.Open) return;
        await _envoi.WaitAsync().ConfigureAwait(false);
        try
        {
            await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true,
                               _cts.Token).ConfigureAwait(false);
        }
        finally { _envoi.Release(); }
    }

    async Task Boucle(ClientWebSocket ws, CancellationToken ct)
    {
        var tampon = new byte[16 * 1024];
        var message = new MemoryStream();   // octets accumulés : un caractère UTF-8 peut tomber à cheval sur deux morceaux
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var res = await ws.ReceiveAsync(new ArraySegment<byte>(tampon), ct).ConfigureAwait(false);
                if (res.MessageType == WebSocketMessageType.Close) { Etat = "fermé par le serveur"; return; }
                message.Write(tampon, 0, res.Count);
                if (!res.EndOfMessage) continue;
                Recus.Enqueue(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                message.SetLength(0);
                Etat = "connecté";
            }
        }
        catch (OperationCanceledException) { Etat = "arrêté"; }
        catch (Exception e) { Etat = "coupé : " + e.Message; }
    }

    async Task Battre(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(Battement, ct).ConfigureAwait(false);
                await Envoyer(MessageBattement(Prochain())).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Etat = "coupé : " + e.Message; }
    }

    // ── Messages (sans réseau : testés en EditMode) ────────────────────────────

    public static string UrlWebSocket(string projetUrl, string cle)
    {
        string hote = (projetUrl ?? "").Trim().TrimEnd('/').Replace("https://", "wss://").Replace("http://", "ws://");
        return hote + "/realtime/v1/websocket?apikey=" + Uri.EscapeDataString(cle ?? "") + "&vsn=1.0.0";
    }

    [Serializable] class Changement { public string @event = "*"; public string schema = "public"; public string table; }
    [Serializable] class Config { public Changement[] postgres_changes; }
    [Serializable] class PayloadJoin { public Config config; public string access_token; }
    [Serializable] class MessageSortant { public string topic; public string @event; public PayloadJoin payload; public string @ref; public string join_ref; }

    /// Demande d'abonnement aux changements des tables, avec le jeton de l'utilisateur
    /// (le serveur applique les règles d'accès de chaque table).
    public static string MessageJoin(string[] tables, string jeton, string reference)
    {
        var chg = new Changement[tables.Length];
        for (int i = 0; i < tables.Length; i++) chg[i] = new Changement { table = tables[i] };
        return JsonUtility.ToJson(new MessageSortant
        {
            topic = Sujet, @event = "phx_join", @ref = reference, join_ref = reference,
            payload = new PayloadJoin { config = new Config { postgres_changes = chg }, access_token = jeton },
        });
    }

    public static string MessageBattement(string reference) =>
        "{\"topic\":\"phoenix\",\"event\":\"heartbeat\",\"payload\":{},\"ref\":\"" + reference + "\"}";

    /// Ligne changée. Champs des deux tables d'essai réunis : JsonUtility remplit ceux qui sont présents.
    [Serializable] public class Enregistrement
    {
        public string id; public double loyer; public string maj_par; public int version;   // essai_fiches
        public string cible, nom, motif, expire_le;                                           // essai_verrous
    }
    [Serializable] public class DonneesChange { public string type, table, commit_timestamp; public Enregistrement record, old_record; }
    [Serializable] public class ReponseServeur { public string reason; }
    [Serializable] public class PayloadEntrant { public string status, message; public DonneesChange data; public ReponseServeur response; }
    [Serializable] public class MessageEntrant { public string topic; public string @event; public PayloadEntrant payload; public string @ref; }

    public static MessageEntrant Lire(string json)
    {
        try { return JsonUtility.FromJson<MessageEntrant>(json); }
        catch { return null; }
    }
}
