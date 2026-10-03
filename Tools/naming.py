"""Kasa Defteri item adindan oyun adini otomatik uretir.

Bir item sitede "Zippo Lighter" ya da "VINTAGE PISTOL" gibi ingilizce / markali / hatali yazilmis bir adla
gelebilir. Bu modul ayni adi oyun icin
  * telifsiz (marka ve model kodlari atilir, yerine genel ad gelir),
  * Turkce ("Metal benzinli çakmak"),
  * iyelikli ("çakmağı": "Stella Kozmo'nun Çakmağı" gibi ünlü sahip adları için) hale getirir,
ve kategori + onizleme sekli onerir. Sozlukte olmayan kelimeler `review` listesine yazilir; orada elle
karar vermek icin Tools/items.json icinde ilgili kaydi duzenleyin.
"""
import difflib
import re

VOWELS = "aeıioöuü"

# ---------------------------------------------------------------- Turkce iyelik
# Tek heceli kelimelerde ve bu yabanci kelimelerde son sessiz yumusamaz.
NO_SOFTEN = {"pikap", "laptop", "kontrplak", "stop", "kask"}
# Duzensiz iyelikler (ses uyumu bozuk ya da tek heceli yumusayanlar)
POSS_EXCEPTIONS = {"plak": "plağı", "renk": "rengi", "saat": "saati", "metal": "metali", "alkol": "alkolü",
                   "kontrol": "kontrolü", "konsol": "konsolu", "petrol": "petrolü", "rol": "rolü", "hal": "hali",
                   "gol": "golü", "terminal": "terminali", "model": "modeli", "pedal": "pedalı", "kalp": "kalbi"}


def syllables(word):
    return sum(1 for c in word if c in VOWELS)


def _harmony(last_vowel):
    return {"a": "ı", "ı": "ı", "e": "i", "i": "i", "o": "u", "u": "u", "ö": "ü", "ü": "ü"}[last_vowel]


def poss3(phrase):
    """Ucuncu tekil iyelik eki: motosiklet -> motosikleti, çakmak -> çakmağı, tabure -> taburesi."""
    phrase = phrase.strip()
    if not phrase:
        return phrase
    head, _, word = phrase.rpartition(" ")
    low = word.lower()
    if low in POSS_EXCEPTIONS:
        return (head + " " if head else "") + POSS_EXCEPTIONS[low]
    vs = [c for c in low if c in VOWELS]
    if not vs:
        return phrase + "i"
    v = _harmony(vs[-1])
    if low[-1] in VOWELS:
        out = word + "s" + v
    else:
        base = word
        if syllables(low) > 1 and low not in NO_SOFTEN:
            if low.endswith("nk"):
                pass
            elif low[-1] == "k":
                base = word[:-1] + "ğ"
            elif low[-1] == "ç":
                base = word[:-1] + "c"
            elif low[-1] == "p":
                base = word[:-1] + "b"
        out = base + v
    return (head + " " if head else "") + out


def title_case_first(text):
    if not text:
        return text
    c = text[0]
    c = "İ" if c == "i" else "I" if c == "ı" else c.upper()
    return c + text[1:]


# ---------------------------------------------------------------- sozluk
# head: ad (kategori, sekil). tr_owned verilmezse poss3(tr) kullanilir; "=" zaten iyelikli bilesik demek.
# format: "ingilizce|turkce|iyelikli(veya bos / =)|kategori|sekil"
HEADS = """
clock|saat||clock|Box
watch|saat||clock|Box
table|masa||furniture|Table
desk|yazı masası||furniture|Table
chair|sandalye||furniture|Chair
stool|tabure||furniture|Box
footstool|tabure||furniture|Box
vitrin|vitrin||furniture|Box
washer|çamaşır makinesi|=|electronics|Box
armchair|koltuk||furniture|Chair
sofa|kanepe||furniture|Box
couch|kanepe||furniture|Box
bed|yatak||furniture|Box
shelf|raf||furniture|Box
bookcase|kitaplık||furniture|Box
cabinet|dolap||furniture|Box
wardrobe|gardırop||furniture|Box
chest|sandık||furniture|Box
trunk|bavul||furniture|Box
mirror|ayna||decor|Mirror
lamp|lamba||decor|Lamp
lantern|fener||decor|Lamp
chandelier|avize||decor|Lamp
vase|vazo||decor|Box
statue|heykel||decor|Box
sculpture|heykel||decor|Box
painting|tablo||decor|Box
candlestick|mumluk||decor|Box
candelabra|şamdan||decor|Box
pipe|pipo||decor|Box
lighter|çakmak||decor|Box
globe|dünya küresi|dünya küresi|decor|Box
bell|çan||decor|Box
frame|çerçeve||decor|Box
ball|top||decor|Box
radio|radyo||electronics|Radio
telephone|telefon||electronics|Box
phone|telefon||electronics|Box
television|televizyon||electronics|Box
tv|televizyon||electronics|Box
computer|bilgisayar||electronics|Box
camera|fotoğraf makinesi|=|electronics|Box
walkietalkie|telsiz||electronics|Radio
typewriter|daktilo||electronics|Box
fan|vantilatör||electronics|Box
heater|soba||electronics|Box
speaker|hoparlör||electronics|Box
gramophone|gramofon||music|Box
record|plak||music|Box
vinyl|plak||music|Box
guitar|gitar||music|Box
piano|piyano||music|Box
violin|keman||music|Box
drum|davul||music|Box
drums|davul seti|davul seti|music|Box
trumpet|trompet||music|Box
saxophone|saksafon||music|Box
dj|DJ seti|DJ seti|music|Box
sword|kılıç||weapon|Box
katana|katana||weapon|Box
pistol|tabanca||weapon|Box
handgun|tabanca||weapon|Box
revolver|altıpatlar||weapon|Box
gun|tüfek||weapon|Box
rifle|tüfek||weapon|Box
shotgun|av tüfeği|=|weapon|Box
smg|hafif makineli tüfek|hafif makineli tüfeği|weapon|Box
knife|bıçak||weapon|Box
dagger|hançer||weapon|Box
axe|balta||weapon|Box
bow|yay||weapon|Box
shield|kalkan||weapon|Box
helmet|kask||weapon|Box
machine|makine||electronics|Box
motor|motor||tools|Box
lighter2|çakmak||decor|Box
arcade|arcade makinesi|=|toy|Box
pinball|langırt makinesi|=|toy|Box
jukebox|müzik kutusu|müzik kutusu|music|Box
fridge|buzdolabı|=|kitchen|Box
refrigerator|buzdolabı|=|kitchen|Box
stove|ocak||kitchen|Box
oven|fırın||kitchen|Box
kettle|çaydanlık||kitchen|Box
teapot|demlik||kitchen|Box
cup|fincan||kitchen|Box
plate|tabak||kitchen|Box
bowl|kase||kitchen|Box
pot|tencere||kitchen|Box
pan|tava||kitchen|Box
mill|değirmen||kitchen|Box
grinder|öğütücü||kitchen|Box
toaster|ekmek kızartma makinesi|=|kitchen|Box
blender|blender||kitchen|Box
bottle|şişe||drink|Box
rum|rom|romu|drink|Box
whisky|viski şişesi|=|drink|Box
whiskey|viski şişesi|=|drink|Box
wine|şarap şişesi|=|drink|Box
motorcycle|motosiklet||vehicle|Box
motorbike|motosiklet||vehicle|Box
scooter|scooter||vehicle|Box
bike|bisiklet||vehicle|Box
bicycle|bisiklet||vehicle|Box
car|otomobil||vehicle|Box
wheel|tekerlek||tools|Box
tire|lastik||tools|Box
flashlight|el feneri|=|tools|Box
torch|el feneri|=|tools|Box
can|kova||tools|Box
bin|kova||tools|Box
bucket|kova||tools|Box
toolbox|alet çantası|=|tools|Box
hammer|çekiç||tools|Box
wrench|anahtar||tools|Box
saw|testere||tools|Box
bag|çanta||tools|Box
backpack|sırt çantası|=|tools|Box
box|kutu||tools|Box
suitcase|valiz||tools|Box
vending|otomat||electronics|Box
vendor|otomat||electronics|Box
dispenser|otomat||electronics|Box
slushy|buzlu içecek makinesi|=|drink|Box
coin|madeni para|madeni parası|decor|Box
medal|madalya||decor|Box
crown|taç||decor|Box
ring|yüzük||decor|Box
necklace|kolye||decor|Box
sewing|dikiş makinesi|=|tools|Box
chopper|motosiklet||vehicle|Box
cruiser|motosiklet||vehicle|Box
moped|moped||vehicle|Box
trophy|kupa||decor|Box
anchor|çapa||decor|Box
telescope|teleskop||electronics|Box
binoculars|dürbün||electronics|Box
compass|pusula||tools|Box
microscope|mikroskop||tools|Box
microwave|mikrodalga fırın||kitchen|Box
skateboard|kaykay||toy|Box
doll|oyuncak bebek|=|toy|Box
teddy|oyuncak ayı|oyuncak ayısı|toy|Box
robot|robot||toy|Box
jar|kavanoz||kitchen|Box
barrel|varil||tools|Box
crate|sandık||tools|Box
carpet|halı||decor|Box
rug|halı||decor|Box
curtain|perde||decor|Box
""".strip().splitlines()

# cok kelimeli ifadeler -> tek kelimeye indirgenir (once islenir)
PHRASES = {
    "vending machine": "vending", "vendor machine": "vending", "trash can": "trashcan", "garbage can": "trashcan",
    "pepper mill": "peppermill", "coffee maker": "coffeemaker", "coffee machine": "coffeemaker",
    "espresso machine": "coffeemaker", "disco ball": "discoball", "walkie talkie": "walkietalkie",
    "pocket watch": "pocketwatch", "wall clock": "wallclock", "grandfather clock": "grandfatherclock",
    "sewing machine": "sewing", "machine gun": "machinegun", "tommy gun": "machinegun", "submachine gun": "smg",
    "drum set": "drums", "dj set": "dj", "ice cold": "icecold", "slush machine": "slushy",
    "slushy machine": "slushy", "dance dance revolution": "ddr", "game ready": "", "high optimized": "",
    "high poly": "", "low poly": "", "china cabinet": "vitrin", "washing machine": "washer", "display case": "vitrin",
    "display cabinet": "vitrin", "ice cream": "icecream",
}
# Birlesik hazir basliklar (yukaridaki indirgenmis anahtarlar icin)
COMBOS = {
    "trashcan": ("çöp kovası", "=", "tools", "Box"),
    "peppermill": ("biber değirmeni", "=", "kitchen", "Box"),
    "coffeemaker": ("kahve makinesi", "=", "kitchen", "Box"),
    "discoball": ("disko topu", "=", "decor", "Box"),
    "pocketwatch": ("cep saati", "=", "clock", "Box"),
    "wallclock": ("duvar saati", "=", "clock", "Box"),
    "grandfatherclock": ("boy saati", "=", "clock", "Box"),
    "machinegun": ("makineli tüfek", "makineli tüfeği", "weapon", "Box"),
    "icecold": None,  # sadece sifat
}
ADJS = {
    "vintage": "vintage", "antique": "antika", "old": "eski", "ancient": "antik", "retro": "retro", "classic": "klasik",
    "modern": "modern", "wooden": "ahşap", "wood": "ahşap", "leather": "deri", "brass": "pirinç", "metal": "metal",
    "steel": "çelik", "iron": "demir", "glass": "cam", "ceramic": "seramik", "porcelain": "porselen", "marble": "mermer",
    "gold": "altın", "golden": "altın", "silver": "gümüş", "copper": "bakır", "military": "askeri", "electronic": "elektronik",
    "electric": "elektrikli", "engraved": "gravürlü", "floral": "çiçekli", "small": "küçük", "large": "büyük",
    "big": "büyük", "tiny": "minik", "rotary": "döner kadranlı", "weathered": "yıpranmış", "decorative": "dekoratif",
    "ornate": "oymalı", "carved": "oymalı", "marquetry": "marketri", "folding": "katlanır", "portable": "taşınabilir",
    
    "cold": "soğuk",
    "sport": "spor", "sports": "spor", "hypersport": "spor", "racing": "yarış", "silenced": "sessiz", "silent": "sessiz",
    "dragon": "ejderhalı", "round": "yuvarlak", "square": "kare", "tall": "uzun", "hanging": "asma", "mechanical": "mekanik",
    "automatic": "otomatik", 
    "red": "kırmızı", "blue": "mavi", "green": "yeşil", "black": "siyah", "white": "beyaz", "yellow": "sarı",
    "pink": "pembe", "purple": "mor", "grey": "gri", "gray": "gri", "brown": "kahverengi",
}
# isim tamlamasi: sonraki baş kelimenin onune gelir ve baş kelime iyelik alir ("duvar saati", "Japon kılıcı")
NMODS = {
    "wall": "duvar", "pocket": "cep", "grandfather": "boy", "gas": "gaz", "coffee": "kahve", "pepper": "biber",
    "candy": "şeker", "gumball": "sakız", "bubblegum": "sakız", "chewing": "sakız", "cigarette": "sigara", "cigar": "puro",
    "soda": "soda", "snack": "atıştırmalık", "ice": "buz", "kitchen": "mutfak", "icecream": "dondurma",
    "street": "sokak", "tea": "çay", "beer": "bira", "toy": "oyuncak", "music": "müzik", "game": "oyun",
    "dance": "dans", "race": "yarış", "tool": "alet", "document": "evrak", "file": "evrak", "shoe": "ayakkabı",
    "fire": "ateş", "water": "su", "cola": "kola", "foot": "ayak", "chess": "satranç", "poker": "poker", "billiard": "bilardo", "pool": "bilardo",
    "japanese": "Japon", "soviet": "Sovyet", "russian": "Rus", "chinese": "Çin", "china": "Çin", "german": "Alman",
    "french": "Fransız", "italian": "İtalyan", "american": "Amerikan", "british": "İngiliz", "turkish": "Türk",
    "brazilian": "Brezilya", "mexican": "Meksika", "spanish": "İspanyol", "indian": "Hint", "ottoman": "Osmanlı",
}
# otomat yapan ilk kelimeler: "candy machine" -> "şeker otomatı"
VENDING_MODS = {"candy", "gumball", "bubblegum", "chewing", "cigarette", "cigar", "soda", "snack", "icecream", "icecold",
                "cola", "beer", "ice", "drink"}
# ayri baş sayilmayan genel kelimeler: baska bir baş varsa dusurulur ("arcade machine" -> "arcade makinesi")
WEAK_HEADS = {"machine", "cabinet", "box", "vending", "dispenser", "vendor"}
# tamamen atilan teknik / model kelimeleri
DROP = {"model", "asset", "remake", "rigged", "stylized", "optimized", "high", "poly", "low", "3d", "game", "ready",
        "old_art", "oldart", "art", "basic", "set", "from", "ep", "the", "of", "and", "with", "in", "for", "by", "a", "an",
        "unity", "pbr", "textured", "version", "v", "new", "final", "scene", "prop", "props", "item", "type", "mk",
        "free", "download", "lowpoly", "hq", "hd", "sketchfab", "blender", "fbx", "obj", "glb", "x", "version"}
# marka / telifli adlar -> genel karsiligi (kelime listesi, sozluk anahtarlariyla). Listede olmayan marka zaten
# sozlukte olmadigi icin basliga hic girmez; bu liste "Zippo -> metal çakmak" gibi anlam kurtarmak icindir.
BRANDS = {
    "zippo": ["metal", "lighter"], "sega": [], "outrun": ["racing", "arcade"], "ddr": ["dance", "arcade"], "konami": [],
    "ferrari": ["sport"], "testarossa": ["sport"], "coca": [], "pepsi": [], "nintendo": [], "atari": [],
    "pacman": ["arcade"], "pong": ["arcade"], "ak47": ["rifle"], "m16": ["rifle"], "vespa": ["scooter"],
    "harley": ["motorcycle"], "davidson": [], "ducati": ["motorcycle"], "yamaha": [], "honda": [], "kawasaki": [],
    "thompson": ["machinegun"], "kalashnikov": ["rifle"], "colt": ["pistol"], "glock": ["pistol"], "beretta": ["pistol"],
    "walther": ["pistol"], "luger": ["pistol"], "browning": [], "rolex": ["watch"], "casio": ["watch"], "seiko": ["watch"],
    "polaroid": ["camera"], "kodak": ["camera"], "leica": ["camera"], "singer": [], "nokia": ["phone"],
    "marlboro": [], "heineken": ["bottle"], "budweiser": ["bottle"], "bacardi": ["rum"], "havana": [],
    "lego": [], "barbie": [], "disney": [], "marvel": [], "pokemon": [], "mario": [], "zelda": [], "sonic": [],
    "playstation": [], "xbox": [], "wii": [], "zil": [], "lada": [], "bakelite": [], "philips": [], "sony": [],
    "samsung": [], "apple": [], "telefunken": ["radio"], "grundig": ["radio"],
}
CYRILLIC = str.maketrans({"с": "c", "а": "a", "е": "e", "о": "o", "р": "p", "х": "x", "у": "y", "к": "k", "м": "m", "т": "t", "н": "h", "в": "b", "і": "i"})

_head = {}
for line in HEADS:
    en, tr, owned, cat, shape = line.split("|")
    _head[en] = (tr, owned or None, cat, shape)
for k, v in COMBOS.items():
    if v:
        _head[k] = (v[0], "=" if v[1] == "=" else v[1], v[2], v[3])
_head.pop("lighter2", None)
_head["vitrin"] = ("vitrin", None, "furniture", "Box")
# Turkce yazilmis site adlari: "Buzdolabı", "Lamba" -> ayni baslik
_TR_ALIAS = {}
for en, (tr, owned, cat, shape) in list(_head.items()):
    if " " not in tr and tr not in _head and tr not in _TR_ALIAS:
        _TR_ALIAS[tr] = en
for tr_word, en in (("lamba", "lamp"), ("sovyet", "soviet"), ("buzdolabı", "fridge"), ("ocak", "stove"),
                    ("mumluk", "candlestick"), ("telsiz", "walkietalkie"), ("çakmak", "lighter"), ("tabure", "stool")):
    _TR_ALIAS[tr_word] = en
VOCAB = (set(_head) | set(ADJS) | set(NMODS) | set(DROP) | set(BRANDS) | set(_TR_ALIAS) | set(PHRASES.values())
         | {"icecold"})
VOCAB.discard("")


def _owned_of(head):
    tr, owned, _, _ = head
    if owned == "=":
        return tr
    return owned if owned else poss3(tr)


def _normalize(token):
    t = token.lower().translate(CYRILLIC)
    return t.replace("i̇", "i")


def _split_compound(t):
    """Bitisik yazilmis iki kelime: gasstove -> gas + stove."""
    for i in range(3, len(t) - 2):
        a, b = t[:i], t[i:]
        a = a[:-1] if a.endswith(b[0]) and len(a) > 3 and a[:-1] in VOCAB else a  # gasstove -> gas|stove
        if a in VOCAB and b in VOCAB and len(b) >= 3:
            return [a, b]
    return None


def _canonical(token):
    """Yazim hatalarini toleransla kelimeye esler: cigarattes -> cigarette, mashine -> machine."""
    t = _normalize(token)
    if t in VOCAB:
        return t
    for suffix in ("s", "es"):
        if t.endswith(suffix) and t[: -len(suffix)] in VOCAB:
            return t[: -len(suffix)]
    if len(t) >= 5:
        m = difflib.get_close_matches(t, VOCAB, n=1, cutoff=0.82)
        if m:
            return m[0]
    return t


def _tokens(name):
    text = name.translate(CYRILLIC)
    text = re.sub(r"(?<=[a-zçğıöşü])(?=[A-ZÇĞİÖŞÜ])", " ", text)  # CamelCase ayir
    text = text.replace("-", " ").replace("_", " ")
    return [t for t in re.split(r"[^A-Za-z0-9İıÖöÜüÇçĞğŞş]+", text) if t]


def _stem(tr):
    """Analiz metninde Turkce eklerle gecen kelimeyi yakalamak icin kok: tüfek -> tüfe, mumluk -> mumlu."""
    w = tr.split()[-1]
    return w[:-1] if w[-1] in "kpçt" and syllables(w) > 1 else w


def _head_from_analysis(analysis):
    text = (analysis or "").lower().replace("i̇", "i")
    best = None
    for en, (tr, _, _, _) in _head.items():
        if en in WEAK_HEADS or en not in _head:
            continue
        m = re.search(r"\b" + re.escape(_stem(tr)), text)
        if m and (best is None or m.start() < best[0]):
            best = (m.start(), en)
    return best[1] if best else None


def name_item(site_name, analysis=""):
    """Site adindan {title, owned, cat, shape, review[]} uretir.

    Baslik yalnizca sozlukteki Turkce karsiliklardan kurulur; sitedeki ham kelimeler (marka, model kodu, yazim hatasi)
    basliga hic girmez. Bu yuzden sonuc yapisi geregi telifsizdir.
    """
    review = []
    raw = _tokens(site_name)
    low = [_normalize(t) for t in raw]
    toks, i = [], 0
    while i < len(low):
        for n in (3, 2):
            seg = low[i:i + n]
            phrase = " ".join(seg)
            if len(seg) == n and phrase in PHRASES:
                if PHRASES[phrase]:
                    toks.append((PHRASES[phrase], True))
                i += n
                break
        else:
            toks.append((raw[i], False))
            i += 1
    adjs, nmods, heads = [], [], []

    def add_unique(lst, v):
        if v not in lst:
            lst.append(v)

    def feed(w):
        w = _TR_ALIAS.get(w, w)
        if w in BRANDS:
            for sub in BRANDS[w]:
                feed(sub)
        elif w in _head:
            if w not in heads:
                heads.append(w)
        elif w == "icecold":
            add_unique(adjs, "soğuk")
            add_unique(nmods, "içecek")
            add_unique(nmods, "__vend__")
        elif w in ADJS:
            add_unique(adjs, ADJS[w])
        elif w in NMODS:
            add_unique(nmods, NMODS[w])
            if w in VENDING_MODS:
                add_unique(nmods, "__vend__")

    for t, done in toks:
        if done:
            feed(t)
            continue
        if re.search(r"\d", t):  # model kodu: M9A3, 1911A1, Type-64
            c = _normalize(t)
            if c in VOCAB:
                feed(c)
            continue
        c = _canonical(t)
        if c in VOCAB:
            feed(c)
            continue
        parts = _split_compound(c)
        if parts:
            for p in parts:
                feed(p)
            continue
        if not (t.isupper() and len(t) <= 4):  # SPY gibi kisaltmalar sessizce atilir, digerleri rapora
            review.append("sözlükte yok: " + t)
    vend = "__vend__" in nmods
    nmods = [m for m in nmods if m != "__vend__"]
    if vend and ("machine" in heads or not heads):
        heads = [h for h in heads if h != "machine"] + ["vending"]
    strong = [h for h in heads if h not in WEAK_HEADS]
    if strong:
        heads = strong
    elif heads:  # yalniz genel kelimeler: en ozel olani birakir (vending > dispenser > cabinet > machine)
        heads = [min(heads, key=lambda h: ("vending", "dispenser", "vendor", "cabinet", "machine", "box").index(h))]
    if not heads:
        guess = _head_from_analysis(analysis)
        if guess:
            heads = [guess]
            review.append("ad analizden çıkarıldı: " + _head[guess][0])
    if not heads:
        review.append("baş isim bulunamadı")
        return {"title": "Eşya", "owned": "eşyası", "cat": "decor", "shape": "Box", "review": review}
    head_key = heads[-1]
    for extra in heads[:-1]:
        if _head[extra][0] != _head[head_key][0]:  # chopper + motorcycle aynı sözcüğe çıkar
            add_unique(nmods, _head[extra][0])
    tr, owned_form, cat, shape = _head[head_key]
    if head_key == "vending" and any(m in ("içecek", "soda", "kola", "bira", "buz") for m in nmods):
        cat = "drink"
    prefix = " ".join(adjs)
    if nmods:
        tail = " ".join(nmods) + " " + (tr if owned_form == "=" else poss3(tr))
        owned_tail = tail
    else:
        tail = tr
        owned_tail = _owned_of(_head[head_key])
    title = title_case_first((prefix + " " + tail).strip())
    owned = (prefix + " " + owned_tail).strip()
    for age in ("vintage ", "antika ", "eski ", "klasik ", "retro ", "yıpranmış "):
        if owned.startswith(age):
            owned = owned[len(age):]
    return {"title": title, "owned": owned, "cat": cat, "shape": shape, "review": review}


def slug(text):
    t = text.lower()
    for a, b in (("ç", "c"), ("ğ", "g"), ("ı", "i"), ("ö", "o"), ("ş", "s"), ("ü", "u"), ("â", "a"), ("î", "i"), ("û", "u")):
        t = t.replace(a, b)
    t = re.sub(r"[^a-z0-9]+", "-", t).strip("-")
    return t[:44] or "esya"


def unique_key(title, taken):
    base = slug(title)
    key, n = base, 2
    while key in taken:
        key = base[:40] + "-" + str(n)
        n += 1
    return key


# ---------------------------------------------------------------- fiyat korumasi
def guard(base, coll, cat, title, kg=None, h=None, w=None, d=None):
    """Sitenin tahminini bariz hatalara karsi duzeltir; (base, coll, notlar). Oyuncak sanilan gercek motosiklet gibi."""
    notes = []
    big = max([x for x in (h, w, d) if x] or [0])
    heavy = kg or 0
    t = title.lower()
    if "motosiklet" in t:
        if base < 3000:
            notes.append("motosiklet taban fiyata çekildi: %d -> 3000" % base)
            base = 3000
        coll = max(coll, 5)
    elif cat == "vehicle" and heavy >= 100 and base < 1500:
        notes.append("ağır araç taban fiyata çekildi: %d -> 1500" % base)
        base = 1500
    if heavy >= 100 and base < 150:
        notes.append("100 kg üstü ama %d$: ölçü veya fiyat hatalı olabilir" % base)
    if big >= 100 and base < 40:
        notes.append("1 metreyi aşan eşya %d$: fiyat düşük olabilir" % base)
    if cat == "toy" and big and big < 20 and base > 300:
        notes.append("küçük oyuncak ama %d$: kontrol et" % base)
    return base, max(0, min(10, coll)), notes
