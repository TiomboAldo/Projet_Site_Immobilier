// Import du fichier CSS principal (généré par Tailwind)
import '../style/input.css';

/* -------------------------------------------
 * GESTION DU MENU MOBILE (Sidebar et Overlay)
 * ------------------------------------------- */
const menuBtn = document.getElementById('menu-btn');
const closeSidebar = document.getElementById('closeSidebar');
const mobileSidebar = document.getElementById('mobileSidebar');
const overlay = document.getElementById('overlay');

// Fonction Ouvrir
const openMenu = () => {
    overlay.classList.remove('hidden');
    setTimeout(() => {
        overlay.classList.add('opacity-100');
    }, 10);
    mobileSidebar.classList.remove('translate-x-full');
    document.body.style.overflow = 'hidden';
};

// Fonction Fermer
const closeMenu = () => {
    overlay.classList.remove('opacity-100');
    mobileSidebar.classList.add('translate-x-full');
    document.body.style.overflow = '';

    setTimeout(() => {
        if (!overlay.classList.contains('opacity-100')) {
            overlay.classList.add('hidden');
        }
    }, 500);
};

if (menuBtn) menuBtn.addEventListener('click', openMenu);
if (closeSidebar) closeSidebar.addEventListener('click', closeMenu);
if (overlay) overlay.addEventListener('click', closeMenu);

/* -------------------------------------------
 * ANIMATION DES SECTIONS (Scroll Reveal)
 * ------------------------------------------- */
const sections = document.querySelectorAll("section");

function checkSectionsScroll() {
    sections.forEach((section) => {
        const position = section.getBoundingClientRect().top;
        const windowHeight = window.innerHeight * 0.8;

        if (position < windowHeight) {
            section.classList.remove("opacity-0", "translate-y-10");
            section.classList.add("opacity-100", "translate-y-0");
        }
    });
}

// Initialisation des classes pour l'état caché (sauf la section "accueil")
sections.forEach((section) => {
    if (section.id && section.id !== 'accueil') {
        section.classList.add(
            "opacity-0",
            "translate-y-10",
            "transition",
            "duration-700",
            "ease-out"
        );
    }
});

window.addEventListener("scroll", checkSectionsScroll);
window.addEventListener('load', checkSectionsScroll);

/* -------------------------------------------
 * DONNÉES DES BIENS
 * ------------------------------------------- */
const properties = [
    { id: 1, title: "Villa de luxe à Douala", type: "villa", status: "vente", price: "350 000 €", beds: 5, baths: 3, size: 400, img: "https://images.unsplash.com/photo-1600585154340-be6161a56a0c", gallery: ["src/assets/images/bien1.jpg", "src/assets/images/bien2.jpg", "src/assets/images/bien3.jpg", "src/assets/images/bien4.jpg"], description: "Magnifique villa moderne située dans le quartier prisé de Bonapriso, offrant une vue imprenable, une piscine privée et des finitions haut de gamme. Idéal pour une famille exigeante.", features: ["Piscine", "Jardin paysager", "Climatisation centralisée", "Garage double", "Système de sécurité"], location: "Douala, Bonapriso", isFavorite: false },
    { id: 2, title: "Appartement moderne à Yaoundé", type: "appartement", status: "location", price: "900 €/mois", beds: 2, baths: 1, size: 120, img: "https://images.unsplash.com/photo-1570129477492-45c003edd2be", description: "Appartement neuf et lumineux en plein cœur du centre-ville. Proche de toutes commodités et transports.", features: ["Balcon", "Cuisine équipée", "Ascenseur", "Concierge"], location: "Yaoundé, Centre-ville", isFavorite: false },
    { id: 3, title: "Maison familiale à Bonapriso", type: "villa", status: "vente", price: "280 000 €", beds: 4, baths: 2, size: 250, img: "https://images.unsplash.com/photo-1507089947368-19c1da9775ae", description: "Charmante maison de style colonial, parfaite pour une vie de famille tranquille. Grand terrain et potentiel d'extension.", features: ["Grand jardin", "Quartier calme", "Proche écoles"], location: "Douala, Bonapriso", isFavorite: false },
    { id: 4, title: "Studio chic à Bonamoussadi", type: "appartement", status: "location", price: "400 €/mois", beds: 1, baths: 1, size: 45, img: "/src/assets/images/bien8.jpg", description: "Studio meublé avec goût, parfait pour étudiant ou jeune professionnel. Tous frais inclus.", features: ["Meublé", "Internet inclus", "Sécurité 24/7"], location: "Douala, Bonamoussadi", isFavorite: false },
    { id: 5, title: "Duplex à Bonanjo", type: "villa", status: "vente", price: "190 000 €", beds: 3, baths: 2, size: 180, img: "/src/assets/images/bien4.jpg", description: "Beau duplex avec terrasse et finitions modernes. Emplacement idéal pour un accès rapide aux affaires.", features: ["Terrasse", "Vue sur la ville", "Proche commodités"], location: "Douala, Bonanjo", isFavorite: false },
    { id: 6, title: "Terrain constructible à Logpom", type: "terrain", status: "vente", price: "120 000 €", beds: 0, baths: 0, size: 1000, img: "/src/assets/images/bien3.jpg", description: "Vaste terrain titré, prêt à construire. Zone résidentielle en pleine expansion.", features: ["Titre foncier disponible", "Zone viabilisée"], location: "Douala, Logpom", isFavorite: false },
    { id: 7, title: "Appartement cosy à Akwa", type: "appartement", status: "location", price: "700 €/mois", beds: 2, baths: 1, size: 90, img: "/src/assets/images/bien9.jpg", description: "Appartement bien entretenu, idéalement situé dans le quartier animé d'Akwa.", features: ["Lumineux", "Rangement intégré"], location: "Douala, Akwa", isFavorite: false },
    { id: 8, title: "Villa à Bastos", type: "villa", status: "vente", price: "420 000 €", beds: 6, baths: 4, size: 500, img: "https://images.unsplash.com/photo-1568605114967-8130f3a36994", description: "Villa de prestige avec de grands volumes et un espace extérieur magnifique. Parfait pour recevoir.", features: ["Piscine", "Cuisine professionnelle", "Salle de sport"], location: "Yaoundé, Bastos", isFavorite: false },
    { id: 9, title: "Appartement à Bonabéri", type: "appartement", status: "location", price: "800 €/mois", beds: 3, baths: 2, size: 150, img: "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267", description: "Spacieux appartement familial dans un immeuble récent et sécurisé.", features: ["3 chambres", "2 salles de bain", "Parking inclus"], location: "Douala, Bonabéri", isFavorite: false },
    { id: 10, title: "Terrain à Kribi", type: "terrain", status: "vente", price: "95 000 €", beds: 0, baths: 0, size: 800, img: "/src/assets/images/bien1.jpg", description: "Terrain proche de la plage, idéal pour un investissement touristique ou une résidence secondaire.", features: ["Proximité mer", "Zone touristique"], location: "Kribi", isFavorite: false },
    { id: 11, title: "Appartement à Bonapriso", type: "appartement", status: "location", price: "450 €/mois", beds: 1, baths: 1, size: 50, img: "/src/assets/images/bien7.jpg", description: "Petit appartement coquet, parfait pour un couple ou une personne seule.", features: ["Quartier recherché", "Petite résidence"], location: "Douala, Bonapriso", isFavorite: false },
    { id: 12, title: "Terrain à Bonamoussadi", type: "terrain", status: "vente", price: "110 000 €", beds: 0, baths: 0, size: 600, img: "https://images.unsplash.com/photo-1599423300746-b62533397364", description: "Terrain en bordure de route, avec accès facile aux grands axes.", features: ["Façade route", "Titre foncier"], location: "Douala, Bonamoussadi", isFavorite: false }
];

/* -------------------------------------------
 * LOGIQUE DE RESERVATION (VERIFICATION CONNEXION)
 * ------------------------------------------- */
let reservationModal;
let modalLoginBtn;
let modalCloseBtn;

window.openReservationModal = function (propertyId) {
    const isAuthenticated = localStorage.getItem('isLoggedIn') === 'true';
    

    if (!isAuthenticated) {
        // --- CAS : PAS CONNECTÉ ---
        localStorage.setItem('propertyIdForReservation', propertyId);
        localStorage.setItem('redirectAfterLogin', 'accueil_user.html');

        if (reservationModal) {
            reservationModal.classList.remove('hidden');
            void reservationModal.offsetWidth;
            reservationModal.classList.add('opacity-100');
            const modalContent = document.getElementById('modalContent');
            if (modalContent) modalContent.classList.remove('scale-95');
        }
        return;
    }

    // --- CAS : CONNECTÉ (On remplace la simulation par du vrai contenu) ---
    const prenom = localStorage.getItem('userPrenom') || "Cher client";
    alert(`Ravi de vous revoir ${prenom} ! Votre demande de réservation pour le bien #${propertyId} a été envoyée avec succès.`);
};

function hideReservationModal() {
    const modalContent = document.getElementById('modalContent');
    if (reservationModal) {
        reservationModal.classList.remove('opacity-100');
        if (modalContent) modalContent.classList.add('scale-95');

        setTimeout(() => {
            reservationModal.classList.add('hidden');
        }, 300);
    }
}

// function setupModalListeners() {
//     reservationModal = document.getElementById('reservationModal');
//     modalLoginBtn = document.getElementById('modalLoginBtn');
//     modalCloseBtn = document.getElementById('modalCloseBtn');

//     if (!reservationModal || !modalLoginBtn || !modalCloseBtn) return;

//     modalLoginBtn.addEventListener('click', () => {
//         // On vérifie si on est déjà dans le dossier /pages/
//         const isInPagesFolder = window.location.pathname.includes('/pages/');

//         let loginPath, detailsPath;

//         if (isInPagesFolder) {
//             // Cas 1 : On est sur detail.html (déjà dans /pages/)
//             loginPath = './login.html';   // Login est juste à côté
//             detailsPath = './accueil_user.html';
//         } else {
//             // Cas 2 : On est sur index.html (à la racine)
//             loginPath = 'src/pages/login.html'; // On doit entrer dans le dossier
//             detailsPath = 'src/pages/accueil_user.html';
//         }

//         localStorage.setItem('redirectAfterLogin', detailsPath);
//         window.location.href = loginPath;
//     });

//     modalCloseBtn.addEventListener('click', hideReservationModal);

//     reservationModal.addEventListener('click', (e) => {
//         if (e.target === reservationModal) {
//             hideReservationModal();
//         }
//     });
// }


function setupModalListeners() {
    reservationModal = document.getElementById('reservationModal');
    modalLoginBtn = document.getElementById('modalLoginBtn');
    modalCloseBtn = document.getElementById('modalCloseBtn');

    if (!reservationModal || !modalLoginBtn || !modalCloseBtn) return;

    modalLoginBtn.addEventListener('click', () => {
        const isInPagesFolder = window.location.pathname.includes('/pages/');

        let loginPath;

        if (isInPagesFolder) {
            // On est sur detail.html
            loginPath = 'login.html';
        } else {
            // On est sur index.html
            loginPath = 'src/pages/login.html';
        }

        // IMPORTANT : On stocke juste le nom du fichier cible, sans chemin complexe
        localStorage.setItem('redirectAfterLogin', 'accueil_user.html');
        window.location.href = loginPath;
    });

    modalCloseBtn.addEventListener('click', hideReservationModal);
    reservationModal.addEventListener('click', (e) => {
        if (e.target === reservationModal) hideReservationModal();
    });
}

function showMessage(title, message, type = 'success') {
    const modal = document.getElementById('infoModal');
    const content = document.getElementById('infoModalContent');
    const iconDiv = document.getElementById('infoIcon');
    const titleH3 = document.getElementById('infoTitle');
    const messageP = document.getElementById('infoMessage');

    // Configuration selon le type
    if (type === 'success') {
        iconDiv.innerHTML = '<i class="fas fa-check-circle text-green-500"></i>';
        titleH3.className = "text-xl font-bold text-green-600 mb-2";
    } else {
        iconDiv.innerHTML = '<i class="fas fa-exclamation-circle text-red-500"></i>';
        titleH3.className = "text-xl font-bold text-red-600 mb-2";
    }

    titleH3.innerText = title;
    messageP.innerText = message;

    // Affichage avec animation
    modal.classList.remove('hidden');
    setTimeout(() => {
        modal.classList.remove('opacity-0');
        content.classList.remove('scale-95');
    }, 10);
}

function closeInfoModal() {
    const modal = document.getElementById('infoModal');
    const content = document.getElementById('infoModalContent');
    modal.classList.add('opacity-0');
    content.classList.add('scale-95');
    setTimeout(() => modal.classList.add('hidden'), 300);
}


/* -------------------------------------------
 * LOGIQUE DES FAVORIS
 * ------------------------------------------- */
window.toggleFavorite = function (id) {
    const property = properties.find(p => p.id === id);
    if (property) {
        property.isFavorite = !property.isFavorite;

        const iconElement = document.getElementById(`favorite-icon-${id}`);
        if (iconElement) {
            if (property.isFavorite) {
                iconElement.classList.remove('far', 'text-gray-400');
                iconElement.classList.add('fas', 'text-red-500');
            } else {
                iconElement.classList.remove('fas', 'text-red-500');
                iconElement.classList.add('far', 'text-gray-400');
            }
        }
    }
};

/* -------------------------------------------
 * LOGIQUE DE RECHERCHE, FILTRAGE & TRI
 * ------------------------------------------- */
const container = document.getElementById('property-grid');
const searchInput = document.getElementById('searchInput');
const filterSelect = document.getElementById('filterSelect');
const statusSelect = document.getElementById('statusSelect');
const sortSelect = document.getElementById('sortSelect');

const itemsPerPage = 6;
let currentPage = 1;
let currentList = properties.slice();
let viewAllBtn;

// ---------- HELPERS ----------
function escapeHtml(str = '') {
    return String(str).replace(/[&<>"']/g, s => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[s]));
}

function capitalize(s = '') {
    return s ? s.charAt(0).toUpperCase() + s.slice(1) : '';
}

function scrollToGrid() {
    container?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

window.viewDetails = function (id) {
    localStorage.setItem('selectedPropertyId', id);
    window.location.href = 'src/pages/details.html';
};

function parsePriceToNumber(priceString) {
    if (!priceString) return 0;
    const cleaned = priceString.replace(/[^0-9]/g, '');
    return parseInt(cleaned) || 0;
}

function sortList(list, sortOrder) {
    if (sortOrder === 'price-asc') {
        return list.sort((a, b) => parsePriceToNumber(a.price) - parsePriceToNumber(b.price));
    } else if (sortOrder === 'price-desc') {
        return list.sort((a, b) => parsePriceToNumber(b.price) - parsePriceToNumber(a.price));
    }
    return list.sort((a, b) => a.id - b.id);
}

// ---------- DEBOUNCE FUNCTION ----------
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// ---------- OBSERVER pour animation d'apparition des cartes ----------
let observer = null;

function observeCards() {
    if (!container) return;
    if (observer) {
        observer.disconnect();
        observer = null;
    }

    observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.remove('opacity-0', 'translate-y-10');
                entry.target.classList.add('opacity-100', 'translate-y-0');
                observer.unobserve(entry.target);
            }
        });
    }, { threshold: 0.12 });

    document.querySelectorAll('.property-card').forEach((el, i) => {
        el.style.transitionDelay = `${i * 0.06}s`;
        observer.observe(el);
    });
}

// ---------- RENDER ----------
function renderPage(page = 1, list = currentList) {
    if (!container) return;

    container.innerHTML = '';
    currentPage = page;

    const start = (page - 1) * itemsPerPage;
    const pageItems = list.slice(start, start + itemsPerPage);

    pageItems.forEach((p) => {
        const typeLabel = (p.type || '').toLowerCase();
        const statutLabel = (p.status || '').toLowerCase();
        const favoriteIconClass = p.isFavorite ? 'fas fa-heart text-red-500' : 'far fa-heart text-gray-400';

        const card = document.createElement('div');
        card.className = 'property-card opacity-0 translate-y-10 transition-all duration-700 ease-out bg-white rounded-2xl shadow-xl overflow-hidden transform hover:-translate-y-2 hover:shadow-2xl';

        card.innerHTML = `
            <div class="overflow-hidden relative">
                <a href="javascript:void(0)" onclick="viewDetails(${p.id})" class="block overflow-hidden">
                    <img src="${escapeHtml(p.img)}" 
                         alt="${escapeHtml(p.title)}" 
                         onerror="this.src='src/assets/images/placeholder.jpg'" 
                         class="w-full h-56 object-cover transition duration-700 ease-out hover:scale-110 cursor-pointer" 
                         loading="lazy" />
                </a>
                
                <span class="absolute top-4 left-4 ${statutLabel === 'location' ? 'bg-green-600' : 'bg-blue-600'} text-white text-sm font-semibold px-4 py-1 rounded-full shadow-md pointer-events-none">
                    ${statutLabel === 'location' ? 'À louer' : 'À vendre'}
                </span>
                <span class="absolute top-4 right-4 bg-black/60 text-white text-xs px-3 py-1 rounded-lg pointer-events-none">${capitalize(typeLabel)}</span>
            </div>
            
            <div class="p-6">
                <div class="flex items-start justify-between">
                    <h3 class="text-xl font-bold text-gray-800 mb-2">${escapeHtml(p.title)}</h3>
                    
                    <button onclick="toggleFavorite(${p.id})" class="p-1 -mt-1 -mr-1 rounded-full hover:bg-red-50 transition" aria-label="Ajouter aux favoris">
                        <i id="favorite-icon-${p.id}" class="${favoriteIconClass} text-xl transition-all duration-300"></i>
                    </button>
                </div>
                
                <p class="text-gray-500 text-sm mb-4 flex items-center space-x-4">
                    <span class="flex items-center"><i class="fas fa-bed mr-2"></i> ${p.beds ?? 0} ch.</span>
                    <span class="flex items-center"><i class="fas fa-bath mr-2"></i> ${p.baths ?? 0} sdb</span>
                    <span class="flex items-center"><i class="fas fa-ruler-combined mr-2"></i> ${p.size ?? 0} m²</span>
                </p>
                <div class="flex items-center justify-between mb-4 border-t border-gray-100 pt-3">
                    <span class="text-2xl font-extrabold ${statutLabel === 'location' ? 'text-green-600' : 'text-blue-600'}">${escapeHtml(p.price)}</span>
                </div>
                <div class="flex items-center justify-between space-x-3">
                    <button onclick="viewDetails(${p.id})" class="flex-1 text-center px-4 py-2 border border-blue-600 text-blue-600 text-sm font-semibold rounded-lg hover:bg-blue-50 transition flex items-center justify-center">
                        Détails <i class="fas fa-info-circle ml-2"></i>
                    </button>
                    <button onclick="openReservationModal(${p.id})" class="flex-1 px-4 py-2 bg-blue-600 text-white text-sm font-semibold rounded-lg hover:bg-blue-700 transition shadow-md flex items-center justify-center">
                        Réserver <i class="fas fa-calendar-check ml-2"></i>
                    </button>
                </div>
            </div>
        `;
        container.appendChild(card);
    });

    if (pageItems.length === 0) {
        container.innerHTML = `<div class="col-span-full text-center py-12 text-gray-500">Aucun bien trouvé pour ces critères.</div>`;
    }

    renderPagination(list);
    observeCards();
}
// Exemple pour le bouton Réserver sur la page détail


// ---------- PAGINATION UI & handlers ----------
function renderPagination(list = currentList) {
    let pagWrapper = document.getElementById('pagination-wrapper');
    if (!pagWrapper) {
        pagWrapper = document.createElement('div');
        pagWrapper.id = 'pagination-wrapper';
        pagWrapper.className = 'max-w-7xl mx-auto mt-8 mb-4 flex justify-center';

        const grid = document.getElementById('property-grid');
        if (grid) grid.after(pagWrapper);
    }
    pagWrapper.innerHTML = '';

    const totalPages = Math.max(1, Math.ceil(list.length / itemsPerPage));
    if (totalPages <= 1) return;

    const nav = document.createElement('nav');
    nav.className = 'flex items-center space-x-2';

    const prevBtn = document.createElement('button');
    prevBtn.innerHTML = '&laquo;';
    prevBtn.className = 'px-3 py-1 border rounded-lg text-gray-600 hover:bg-gray-200 disabled:opacity-50';
    prevBtn.disabled = currentPage <= 1;
    prevBtn.addEventListener('click', () => {
        if (currentPage > 1) {
            currentPage--;
            renderPage(currentPage, list);
            scrollToGrid();
        }
    });
    nav.appendChild(prevBtn);

    const maxVisiblePages = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
    let endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);
    if (endPage - startPage + 1 < maxVisiblePages) {
        startPage = Math.max(1, endPage - maxVisiblePages + 1);
    }

    for (let i = startPage; i <= endPage; i++) {
        const btn = document.createElement('button');
        btn.textContent = i;
        btn.className = `page-btn px-4 py-2 border rounded-full font-semibold transition ${i === currentPage ? 'bg-blue-600 text-white' : 'hover:bg-gray-200 text-gray-700'}`;
        btn.addEventListener('click', () => {
            currentPage = i;
            renderPage(currentPage, list);
            scrollToGrid();
        });
        nav.appendChild(btn);
    }

    const nextBtn = document.createElement('button');
    nextBtn.innerHTML = '&raquo;';
    nextBtn.className = 'px-3 py-1 border rounded-lg text-gray-600 hover:bg-gray-200 disabled:opacity-50';
    nextBtn.disabled = currentPage >= totalPages;
    nextBtn.addEventListener('click', () => {
        if (currentPage < totalPages) {
            currentPage++;
            renderPage(currentPage, list);
            scrollToGrid();
        }
    });
    nav.appendChild(nextBtn);
    pagWrapper.appendChild(nav);
}

// ---------- FILTRAGE, RECHERCHE & TRI DYNAMIQUE ----------
function applyFilters() {
    const q = (searchInput?.value || '').trim().toLowerCase();
    const type = (filterSelect?.value || '').trim().toLowerCase();
    const status = (statusSelect?.value || '').trim().toLowerCase();
    const sort = (sortSelect?.value || 'default').trim();

    let filteredList = properties.filter(p => {
        const title = (p.title || '').toLowerCase();
        const matchesQ = !q || title.includes(q) || (p.location || '').toLowerCase().includes(q);
        const matchesType = !type || (p.type || '').toLowerCase() === type;
        const matchesStatus = !status || (p.status || '').toLowerCase() === status;
        return matchesQ && matchesType && matchesStatus;
    });

    currentList = sortList(filteredList, sort);
    currentPage = 1;
    renderPage(1, currentList);
}

function attachFilterListeners() {
    // Utilise debounce pour la recherche (optimisation)
    if (searchInput) searchInput.addEventListener('input', debounce(applyFilters, 300));
    if (filterSelect) filterSelect.addEventListener('change', applyFilters);
    if (statusSelect) statusSelect.addEventListener('change', applyFilters);
    if (sortSelect) sortSelect.addEventListener('change', applyFilters);
}

/* -------------------------------------------
 * GESTION DU POP-UP DE CONTACT
 * ------------------------------------------- */
const contactModal = document.getElementById('contactModal');
const contactModalContent = document.getElementById('contactModalContent');
const openContactBtn = document.getElementById('openContactBtn');
const closeContactBtn = document.getElementById('closeContactBtn');
const contactForm = document.getElementById('contactForm');

const openModal = () => {
    if (!contactModal) return;
    contactModal.classList.remove('hidden');
    setTimeout(() => {
        contactModal.classList.add('opacity-100');
        if (contactModalContent) {
            contactModalContent.classList.remove('scale-95');
            contactModalContent.classList.add('scale-100');
        }
    }, 10);
};

const closeModal = () => {
    if (!contactModal) return;
    contactModal.classList.remove('opacity-100');
    if (contactModalContent) {
        contactModalContent.classList.remove('scale-100');
        contactModalContent.classList.add('scale-95');
    }
    setTimeout(() => {
        contactModal.classList.add('hidden');
    }, 300);
};

if (openContactBtn) openContactBtn.addEventListener('click', openModal);
if (closeContactBtn) closeContactBtn.addEventListener('click', closeModal);

if (contactModal) {
    contactModal.addEventListener('click', (e) => {
        if (e.target === contactModal) closeModal();
    });
}

// Gestion de l'envoi du formulaire avec validation
if (contactForm) {
    contactForm.addEventListener('submit', (e) => {
        e.preventDefault();

        // Récupérer les valeurs
        const nameInput = contactForm.querySelector('input[type="text"]');
        const emailInput = contactForm.querySelector('input[type="email"]');
        const messageInput = contactForm.querySelector('textarea');

        const name = nameInput?.value.trim() || '';
        const email = emailInput?.value.trim() || '';
        const message = messageInput?.value.trim() || '';

        // Validation
        if (!name || name.length < 2) {
            alert("Veuillez entrer un nom valide (minimum 2 caractères)");
            nameInput?.focus();
            return;
        }

        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email)) {
            alert("Veuillez entrer une adresse email valide");
            emailInput?.focus();
            return;
        }

        if (!message || message.length < 10) {
            alert("Votre message doit contenir au moins 10 caractères");
            messageInput?.focus();
            return;
        }

        // Simulation d'envoi
        const submitBtn = contactForm.querySelector('button[type="submit"]');
        if (submitBtn) {
            const originalHTML = submitBtn.innerHTML;
            submitBtn.innerHTML = 'Envoi en cours... <i class="fas fa-spinner fa-spin ml-2"></i>';
            submitBtn.disabled = true;

            setTimeout(() => {
                alert("Merci ! Votre message a été envoyé avec succès. Nous vous contacterons bientôt.");
                contactForm.reset();
                submitBtn.innerHTML = originalHTML;
                submitBtn.disabled = false;
                closeModal();
            }, 1500);
        }
    });
}

/* -------------------------------------------
 * GESTION DU CLAVIER (ACCESSIBILITÉ)
 * ------------------------------------------- */
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        // Fermer la modale de réservation
        if (reservationModal && !reservationModal.classList.contains('hidden')) {
            hideReservationModal();
        }
        // Fermer la modale de contact
        if (contactModal && !contactModal.classList.contains('hidden')) {
            closeModal();
        }
        // Fermer le menu mobile
        if (mobileSidebar && !mobileSidebar.classList.contains('translate-x-full')) {
            closeMenu();
        }
    }
});

/* -------------------------------------------
 * BOUTON RETOUR EN HAUT (SCROLL TO TOP)
 * ------------------------------------------- */
function createScrollTopButton() {
    const scrollTopBtn = document.createElement('button');
    scrollTopBtn.id = 'scrollTopBtn';
    scrollTopBtn.className = 'fixed bottom-8 right-8 bg-blue-600 text-white w-12 h-12 rounded-full shadow-lg hover:bg-blue-700 transition-all duration-300 z-40 hidden flex items-center justify-center';
    scrollTopBtn.innerHTML = '<i class="fas fa-arrow-up"></i>';
    scrollTopBtn.setAttribute('aria-label', 'Retour en haut');

    scrollTopBtn.addEventListener('click', () => {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });

    document.body.appendChild(scrollTopBtn);

    // Afficher/masquer selon le scroll
    window.addEventListener('scroll', () => {
        if (window.scrollY > 500) {
            scrollTopBtn.classList.remove('hidden');
            scrollTopBtn.classList.add('flex');
        } else {
            scrollTopBtn.classList.add('hidden');
            scrollTopBtn.classList.remove('flex');
        }
    });
}

/* -------------------------------------------
 * INITIALISATION
 * ------------------------------------------- */
document.addEventListener('DOMContentLoaded', () => {
    const originalViewAllBtnContainer = document.querySelector('#biens > div.text-center');
    if (originalViewAllBtnContainer) {
        viewAllBtn = originalViewAllBtnContainer;
        viewAllBtn.classList.add('hidden', 'mt-4');
    }

    setupModalListeners();
    renderPage(1, currentList);
    attachFilterListeners();
    createScrollTopButton();
});

const header = document.getElementById('main-header');
const logo = document.getElementById('main-logo');

window.addEventListener('scroll', () => {
    if (window.scrollY > 50) {
        // État réduit lors du scroll
        header.classList.replace('py-4', 'py-2');
        header.classList.add('shadow-xl'); // Ombre plus prononcée en scrollant

        logo.classList.replace('h-16', 'h-10');
        logo.classList.replace('md:h-20', 'md:h-12');
    } else {
        // État initial (Bannière haute)
        header.classList.replace('py-2', 'py-4');
        header.classList.remove('shadow-xl');

        logo.classList.replace('h-10', 'h-16');
        logo.classList.replace('md:h-12', 'md:h-20');
    }
});
document.addEventListener('DOMContentLoaded', () => {
    const slides = document.querySelectorAll('.hero-slide');
    const prevBtn = document.getElementById('prevSlide');
    const nextBtn = document.getElementById('nextSlide');
    let currentIndex = 0;
    const intervalTime = 5000;
    let slideInterval;

    function showSlide(index) {
        slides.forEach((slide, i) => {
            if (i === index) {
                slide.classList.replace('opacity-0', 'opacity-100');
                slide.classList.add('active'); // Déclenche le zoom Ken Burns
            } else {
                slide.classList.replace('opacity-100', 'opacity-0');
                slide.classList.remove('active'); // Réinitialise le zoom
            }
        });
        currentIndex = index;
    }

    function handleNext() {
        showSlide((currentIndex + 1) % slides.length);
    }

    function handlePrev() {
        showSlide((currentIndex - 1 + slides.length) % slides.length);
    }

    const startAutoSlide = () => {
        slideInterval = setInterval(handleNext, intervalTime);
    };

    const resetTimer = () => {
        clearInterval(slideInterval);
        startAutoSlide();
    };

    nextBtn.addEventListener('click', () => { handleNext(); resetTimer(); });
    prevBtn.addEventListener('click', () => { handlePrev(); resetTimer(); });

    startAutoSlide();
});