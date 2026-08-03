// Module API partagé — utilisé par main.js, details.html, etc.
const API_BASE = '/api';

async function apiFetch(url, options = {}) {
    const token = localStorage.getItem('jwtToken');
    try {
        const res = await fetch(url, {
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
            },
            ...options,
        });
        return await res.json();
    } catch (err) {
        console.error('API error:', url, err);
        return { success: false, message: 'Erreur réseau : le serveur ne répond pas.' };
    }
}

export function getBiens(params = {}) {
    const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== '');
    const query = new URLSearchParams(entries).toString();
    return apiFetch(`${API_BASE}/biens${query ? '?' + query : ''}`);
}

export function getBienById(id) {
    return apiFetch(`${API_BASE}/biens/${id}`);
}

export function createReservation(payload) {
    return apiFetch(`${API_BASE}/reservations`, {
        method: 'POST',
        body: JSON.stringify(payload),
    });
}

export function getFavorisByUser(userId) {
    return apiFetch(`${API_BASE}/favoris/user/${userId}`);
}

export function addFavori(userId, bienId) {
    return apiFetch(`${API_BASE}/favoris`, {
        method: 'POST',
        body: JSON.stringify({ userId, bienId }),
    });
}

export function removeFavori(userId, bienId) {
    return apiFetch(`${API_BASE}/favoris/${userId}/${bienId}`, { method: 'DELETE' });
}

export function getCommentaires(bienId) {
    return apiFetch(`${API_BASE}/commentaires?bienId=${bienId}`);
}

export function createCommentaire(payload) {
    return apiFetch(`${API_BASE}/commentaires`, {
        method: 'POST',
        body: JSON.stringify(payload),
    });
}

export function updateCommentaire(id, payload) {
    return apiFetch(`${API_BASE}/commentaires/${id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
    });
}

export function deleteCommentaire(id) {
    return apiFetch(`${API_BASE}/commentaires/${id}`, { method: 'DELETE' });
}

export function recommanderBien(payload) {
    return apiFetch(`${API_BASE}/recommandations`, {
        method: 'POST',
        body: JSON.stringify(payload),
    });

}

export function getRecommandationsRecues() {
    return apiFetch(`${API_BASE}/recommandations/recues`);
}

export function marquerRecommandationLue(id) {
    return apiFetch(`${API_BASE}/recommandations/${id}/lue`, { method: 'PUT' });
}

export function deleteRecommandation(id) {
    return apiFetch(`${API_BASE}/recommandations/${id}`, { method: 'DELETE' });
}

// Adapte un BienDto (champs C# en PascalCase->camelCase) vers le format attendu par le rendu front
export function normalizeBien(b) {
    return {
        id: b.id,
        title: b.titre,
        type: b.type,
        status: b.statut,
        price: b.prix,
        beds: b.chambres,
        baths: b.sallesDeBain,
        size: b.surface,
        img: b.imageUrl,
        gallery: (b.galerie && b.galerie.length) ? b.galerie : [b.imageUrl],
        location: b.localisation,
        description: b.description,
        features: b.equipements || [],
        standing: b.standing,
        proprietaireId:        b.proprietaireId ?? null,
        telephoneProprietaire: b.telephoneProprietaire ?? null,
        lat: b.latitude  ?? null,
        lng: b.longitude ?? null,
        vues: b.vues ?? 0,
        likes: b.likes ?? 0,
        estLikeParMoi: b.estLikeParMoi ?? false,
    };
}

export function likerBien(id) {
    return apiFetch(`${API_BASE}/biens/${id}/like`, { method: 'POST' });
}

export function sendMessage(destinataireId, bienId, contenu) {
    return apiFetch(`${API_BASE}/messages`, {
        method: 'POST',
        body: JSON.stringify({ DestinataireId: destinataireId, BienId: bienId, Contenu: contenu }),
    });
}

export { API_BASE };
