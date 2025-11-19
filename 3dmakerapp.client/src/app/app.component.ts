import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

interface Product {
  id?: string;
  name: string;
  description: string;
  price: number;
  image?: string | null; // base64 full image
  imageContentType?: string | null;
  thumbnailCard?: string | null; // base64 thumbnail for card
  thumbnailCardContentType?: string | null;
  thumbnailDetail?: string | null; // base64 thumbnail for detail modal
  thumbnailDetailContentType?: string | null;
}

interface CartItem {
  productId: string;
  name: string;
  description: string;
  price: number;
  quantity: number;
  thumbnailCard?: string | null;  // Caricata dinamicamente
  thumbnailCardContentType?: string | null;  // Caricata dinamicamente
}

interface ProductButtonState {
  expanded: boolean;
  loading: boolean;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit, OnDestroy {
  title = '3dmakerapp.client';
  public products: Product[] = [];

  public lastError: string | null = null;
  public lastSuccess: string | null = null;

  public search: string = '';
  public nameFilter: string = '';
  public sortBy: string = '';
  public names: string[] = [];

  public page = 1;
  public pageSize = 12;
  public total = 0;

  // Search debounce
  private searchSubject = new Subject<string>();
  private searchSubscription?: Subscription;

  // new product form fields
  public newName = '';
  public newDescription = '';
  public newPrice: string = ''; // keep as string to preserve comma/dot
  public selectedFile: File | null = null;
  public selectedPreviewUrl: string | null = null;
  public selectedFileError: string | null = null; // validation error for file

  public selectedProduct: Product | null = null; // for detail modal

  // theme settings
  public theme: 'light' | 'dark' = 'dark';

  // cart state and per-product quantity
  public cart: CartItem[] = [];
  private productQuantities: Record<string, number> = {};

  public cartOpen = false; // show cart modal
  public cartClosing = false; // track closing animation
  public menuOpen = false; // mobile menu state
  public showClearCartConfirm = false; // show clear cart confirmation modal
  public showFilterModal = false; // show filter modal on mobile
  public showSortModal = false; // show sort modal on mobile

  // track images that failed to load so we can show placeholder
  public imageFailed: Record<string, boolean> = {};

  // track button states per product
  public buttonStates: Record<string, ProductButtonState> = {};

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.loadNames();
    this.loadProducts();

    const storedTheme = localStorage.getItem('theme');
    if (storedTheme === 'light' || storedTheme === 'dark') this.theme = storedTheme as 'light' | 'dark';
    else this.theme = 'dark';

    this.applyTheme();

    // restore cart (without images)
    try {
      const raw = localStorage.getItem('cart');
      if (raw) {
        this.cart = JSON.parse(raw) as CartItem[];
        // Images will be loaded from DB when cart is opened
        console.log('Cart restored from localStorage (images will be loaded from DB):', this.cart.length, 'items');
      }
    } catch {}

    // Search subscription
    this.searchSubscription = this.searchSubject
      .pipe(
        debounceTime(300), // wait for 300ms pause in events
        distinctUntilChanged() // only emit if value is different from previous
      )
      .subscribe((searchTerm) => {
        this.search = searchTerm;
        this.loadProducts(1);
      });
  }

  ngOnDestroy(): void {
    this.searchSubscription?.unsubscribe();
  }

  applyTheme() {
    document.body.classList.remove('theme-light', 'theme-dark');
    document.body.classList.add(this.theme === 'dark' ? 'theme-dark' : 'theme-light');
    // persist
    localStorage.setItem('theme', this.theme);
  }

  toggleTheme() {
    this.theme = this.theme === 'dark' ? 'light' : 'dark';
    this.applyTheme();
  }

  private loadNames() {
    this.http.get<string[]>('/api/products/names').subscribe({
      next: (names) => (this.names = names || []),
      error: (err) => {
        console.warn('Failed to load names for filter', err);
        // fallback to loading all and extracting names
        this.http.get<Product[]>('/api/products').subscribe({
          next: (all) => {
            const set = new Set<string>();
            for (const p of all) set.add(p.name);
            this.names = Array.from(set).sort();
          },
          error: () => {}
        });
      }
    });
  }

  loadProducts(page: number = 1) {
    this.lastError = null;
    this.page = page;

    let params = new HttpParams()
      .set('page', this.page.toString())
      .set('pageSize', this.pageSize.toString());

    if (this.search) params = params.set('search', this.search);
    if (this.nameFilter) params = params.set('name', this.nameFilter);
    if (this.sortBy) params = params.set('sortBy', this.sortBy);

    this.http.get<any>('/api/products/query', { params }).subscribe({
      next: (res) => {
        this.products = res.items || [];
        this.total = res.total || 0;
        // ensure default quantities exist
        for (const p of this.products) {
          if (p.id && !this.productQuantities[p.id]) this.productQuantities[p.id] = 1;
          // reset imageFailed for new items
          if (p.id && this.imageFailed[p.id]) delete this.imageFailed[p.id];
        }
      },
      error: (err) => {
        console.error('Failed to load products', err);
        this.lastError = 'Impossibile caricare i prodotti dal server.';
      }
    });
  }

  onSearch() {
    this.searchSubject.next(this.search);
  }

  onSearchInput(value: string) {
    this.searchSubject.next(value);
  }

  onFilterChange() {
    this.loadProducts(1);
  }

  onSortChange() {
    this.loadProducts(1);
  }

  goToPage(p: number) {
    if (p < 1) p = 1;
    const last = this.pageCount;
    if (p > last) p = last;
    this.loadProducts(p);
  }

  get pageCount() {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  // quantity helpers
  getQty(p: Product) {
    if (!p.id) return 1;
    return this.productQuantities[p.id] ?? 1;
  }

  incQty(p: Product) {
    if (!p.id) return;
    const current = this.productQuantities[p.id] ?? 1;
    this.productQuantities[p.id] = Math.min(99, current + 1);
  }

  decQty(p: Product) {
    if (!p.id) return;
    const current = this.productQuantities[p.id] ?? 1;
    this.productQuantities[p.id] = Math.max(1, current - 1);
  }

  // cart operations
  getButtonState(p: Product): ProductButtonState {
    if (!p.id) return { expanded: false, loading: false };
    if (!this.buttonStates[p.id]) {
      this.buttonStates[p.id] = { expanded: false, loading: false };
    }
    return this.buttonStates[p.id];
  }

  addToCart(p: Product, event?: MouseEvent) {
    if (event) event.stopPropagation();
    if (!p.id) return;
    
    const state = this.getButtonState(p);
    if (!state) return;
    
    state.loading = true;
    
    setTimeout(() => {
      const qty = this.getQty(p);
      const existing = this.cart.find(c => c.productId === p.id!);
      if (existing) {
        existing.quantity = Math.min(999, existing.quantity + qty);
      } else {
        if (p.id) {
          // Save only product metadata, NOT the image
          this.cart.push({ 
            productId: p.id, 
            name: p.name, 
            description: p.description,
            price: p.price, 
            quantity: qty
            // thumbnailCard and thumbnailCardContentType will be loaded from DB when cart opens
          });
        }
      }
      this.saveCart();
      
      state.loading = false;
    }, 800);
  }

  saveCart() {
    try { localStorage.setItem('cart', JSON.stringify(this.cart)); } catch {}
  }

  // cart UI
  openCart() { 
    this.cartOpen = true;
    this.cartClosing = false;
    // Load product images for cart items from DB
    this.loadCartItemImages();
  }
  
  closeCart() { 
    this.cartClosing = true;
    setTimeout(() => {
      this.cartOpen = false;
      this.cartClosing = false;
    }, 300); // match animation duration
  }

  private loadCartItemImages() {
    // Load images for all cart items that don't have them yet
    this.cart.forEach(item => {
      if (!item.thumbnailCard && item.productId) {
        this.http.get<Product>(`/api/products/${item.productId}`).subscribe({
          next: (product) => {
            if (product.thumbnailCard && product.thumbnailCardContentType) {
              item.thumbnailCard = product.thumbnailCard;
              item.thumbnailCardContentType = product.thumbnailCardContentType;
            }
          },
          error: (err) => {
            console.error(`Failed to load image for product ${item.productId}`, err);
          }
        });
      }
    });
  }

  clearCart() {
    this.showClearCartConfirm = true;
  }

  confirmClearCart() {
    this.cart = [];
    this.saveCart();
    this.showClearCartConfirm = false;
  }

  cancelClearCart() {
    this.showClearCartConfirm = false;
  }

  toggleMenu() {
    this.menuOpen = !this.menuOpen;
  }

  closeMenu() {
    this.menuOpen = false;
  }

  openFilterModal() {
    this.showFilterModal = true;
  }

  closeFilterModal() {
    this.showFilterModal = false;
  }

  selectFilter(name: string) {
    this.nameFilter = name;
    this.closeFilterModal();
    this.onFilterChange();
  }

  openSortModal() {
    this.showSortModal = true;
  }

  closeSortModal() {
    this.showSortModal = false;
  }

  selectSort(sortBy: string) {
    this.sortBy = sortBy;
    this.closeSortModal();
    this.onSortChange();
  }

  getSortLabel(sortBy: string): string {
    const labels: Record<string, string> = {
      'newest': 'Ultimi inseriti',
      'price-asc': 'Prezzo: crescente',
      'price-desc': 'Prezzo: decrescente',
      'name-asc': 'Nome: A-Z',
      'name-desc': 'Nome: Z-A'
    };
    return labels[sortBy] || 'Ordina per...';
  }

  removeFromCart(item: CartItem) {
    this.cart = this.cart.filter(c => c.productId !== item.productId);
    this.saveCart();
  }

  changeCartQty(item: CartItem, qty: number) {
    const v = Math.max(1, Math.min(999, qty));
    const found = this.cart.find(c => c.productId === item.productId);
    if (found) {
      found.quantity = v;
      this.saveCart();
    }
  }

  get cartCount() {
    return this.cart.reduce((s, c) => s + c.quantity, 0);
  }

  get cartTotal() {
    return this.cart.reduce((s, c) => s + c.quantity * c.price, 0);
  }

  // image error handler
  onImageError(p: Product | null) {
    if (!p?.id) return;
    this.imageFailed[p.id] = true;
  }

  // file selection handler with client-side validation and resized preview
  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedFileError = null;
    this.selectedFile = null;
    this.selectedPreviewUrl = null;

    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      const allowedTypes = ['image/png', 'image/jpeg'];
      const maxSize = 5 * 1024 * 1024; // 5 MB

      if (!allowedTypes.includes(file.type)) {
        this.selectedFileError = 'Sono consentite solo immagini PNG o JPEG.';
        return;
      }

      const ext = file.name.split('.').pop()?.toLowerCase() || '';
      if (!['png', 'jpg', 'jpeg'].includes(ext)) {
        this.selectedFileError = 'Estensione file non valida. Usa png o jpg.';
        return;
      }

      if (file.size > maxSize) {
        this.selectedFileError = 'Il file è troppo grande. Max 5 MB.';
        return;
      }

      // We'll resize the image to the target thumb dimensions to ensure consistency
      const reader = new FileReader();
      reader.onload = (e) => {
        const img = new Image();
        img.onload = () => {
          // target thumbnail size (same for all cards)
          const targetSize = 160; // px (width and height)

          // Calculate new dimensions preserving aspect ratio and then draw centered on square canvas
          let srcW = img.width;
          let srcH = img.height;

          // compute scale so the smaller side fills the square and we can crop/cover
          const scale = Math.max(targetSize / srcW, targetSize / srcH);
          const drawW = Math.round(srcW * scale);
          const drawH = Math.round(srcH * scale);

          const canvas = document.createElement('canvas');
          canvas.width = targetSize;
          canvas.height = targetSize;
          const ctx = canvas.getContext('2d');
          if (ctx) {
            // draw the scaled image centered so it behaves like object-fit: cover
            const offsetX = Math.round((targetSize - drawW) / 2);
            const offsetY = Math.round((targetSize - drawH) / 2);
            ctx.fillStyle = 'transparent';
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.drawImage(img, offsetX, offsetY, drawW, drawH);

            // preview as data URL
            try {
              const dataUrl = canvas.toDataURL(file.type);
              this.selectedPreviewUrl = dataUrl;
            } catch (err) {
              this.selectedPreviewUrl = URL.createObjectURL(file);
            }

            // convert canvas to blob and use that as the file to upload (so server stores consistent size)
            canvas.toBlob((blob) => {
              if (blob) {
                try {
                  const resizedFile = new File([blob], file.name, { type: file.type });
                  this.selectedFile = resizedFile;
                } catch {
                  // fallback for older browsers
                  this.selectedFile = blob as unknown as File;
                }
              }
            }, file.type);
          } else {
            // fallback
            this.selectedPreviewUrl = URL.createObjectURL(file);
            this.selectedFile = file;
          }
        };
        img.src = e.target?.result as string;
      };
      reader.readAsDataURL(file);
    }
  }

  // limit price input to at most 2 decimals and sanitize characters
  onPriceInput() {
    let s = this.newPrice ?? '';
    if (!s) return;

    // keep only digits and separators
    s = s.replace(/[^0-9.,-]/g, '');

    // find last separator (dot or comma)
    const lastDot = s.lastIndexOf('.');
    const lastComma = s.lastIndexOf(',');
    const sepIndex = Math.max(lastDot, lastComma);

    if (sepIndex === -1) {
      // no decimal separator
      // remove any other separators just in case
      this.newPrice = s.replace(/[.,]/g, '');
      return;
    }

    const sep = s[sepIndex];
    let intPart = s.slice(0, sepIndex).replace(/[.,]/g, '');
    let frac = s.slice(sepIndex + 1).replace(/[.,]/g, '');

    if (frac.length > 2) {
      frac = frac.slice(0, 2);
    }

    this.newPrice = intPart + sep + frac;
  }

  // create product using multipart/form-data
  createProduct() {
    this.lastError = null;
    if (!this.newName) {
      this.lastError = 'Il nome è obbligatorio.';
      return;
    }

    if (this.selectedFileError) {
      this.lastError = this.selectedFileError;
      return;
    }

    // basic client-side price validation: accept both comma and dot
    const priceVal = this.newPrice?.toString() ?? '';
    const normalized = priceVal.replace(',', '.');
    const parsed = Number(normalized);
    if (priceVal && (isNaN(parsed) || !isFinite(parsed))) {
      this.lastError = 'Formato prezzo non valido.';
      return;
    }

    const formData = new FormData();
    formData.append('Name', this.newName);
    formData.append('Description', this.newDescription);
    formData.append('Price', this.newPrice || '0');
    if (this.selectedFile) {
      formData.append('Image', this.selectedFile, this.selectedFile.name);
    }

    this.http.post<Product>('/api/products', formData).subscribe({
      next: (created) => {
        this.resetNewProduct();
        // refresh list and names
        this.loadNames();
        this.loadProducts(1);
      },
      error: (err) => {
        console.error('Failed to create product', err);
        this.lastError = 'Impossibile creare il prodotto.';
      }
    });
  }

  resetNewProduct() {
    this.newName = '';
    this.newDescription = '';
    this.newPrice = '';
    this.selectedFile = null;
    this.selectedFileError = null;
    if (this.selectedPreviewUrl) {
      URL.revokeObjectURL(this.selectedPreviewUrl);
      this.selectedPreviewUrl = null;
    }
  }

  // Reset filters and reload all products
  resetFilters() {
    this.search = '';
    this.nameFilter = '';
    this.sortBy = '';
    this.loadProducts(1);
  }

  // Detail modal
  openDetail(p: Product) {
    this.selectedProduct = p;
  }

  openDetailFromCart(item: CartItem) {
    // Close cart immediately for better UX
    this.closeCart();
    
    // Load product in parallel - if we already have it in products list, use it
    const existingProduct = this.products.find(p => p.id === item.productId);
    
    if (existingProduct) {
      // Open detail immediately with cached product
      setTimeout(() => {
        this.selectedProduct = existingProduct;
      }, 350);
    } else {
      // Load from server but still open quickly
      setTimeout(() => {
        // Show loading state with placeholder data
        this.selectedProduct = {
          id: item.productId,
          name: item.name,
          description: item.description,
          price: item.price
        } as Product;
      }, 350);
      
      // Then load full details in background
      this.http.get<Product>(`/api/products/${item.productId}`).subscribe({
        next: (product) => {
          if (this.selectedProduct?.id === item.productId) {
            this.selectedProduct = product;
          }
        },
        error: (err) => {
          console.error(`Failed to load product ${item.productId}`, err);
        }
      });
    }
  }

  closeDetail() {
    this.selectedProduct = null;
  }

  openFullImage(p: Product | null) {
    if (!p) return;
    if (p.image && p.imageContentType) {
      const url = 'data:' + p.imageContentType + ';base64,' + p.image;
      const win = window.open();
      if (win) {
        win.document.write('<title>' + (p.name || 'Image') + '</title>');
        win.document.write('<img src="' + url + '" style="max-width:100%;height:auto;display:block;margin:0 auto;"/>');
      }
    }
  }

  // helper to get card image/data url (handles different JSON property casings)
  getCardSrc(p: any): string | null {
    if (!p) return null;
    const data = p.thumbnailCard ?? p.ThumbnailCard ?? p.image ?? p.Image ?? null;
    const type = p.thumbnailCardContentType ?? p.ThumbnailCardContentType ?? p.imageContentType ?? p.ImageContentType ?? null;
    if (!data || !type) return null;
    return `data:${type};base64,${data}`;
  }

  // helper for detail image src
  getDetailSrc(p: any): string | null {
    if (!p) return null;
    const data = p.thumbnailDetail ?? p.ThumbnailDetail ?? p.image ?? p.Image ?? null;
    const type = p.thumbnailDetailContentType ?? p.ThumbnailDetailContentType ?? p.imageContentType ?? p.ImageContentType ?? null;
    if (!data || !type) return null;
    return `data:${type};base64,${data}`;
  }

  // helper to check if image failed for product (avoids indexing with undefined)
  isImageFailed(p: any): boolean {
    if (!p) return false;
    const id = p.id ?? p.Id ?? null;
    if (!id) return false;
    return !!this.imageFailed[id];
  }

  getItemImageSrc(item: CartItem): string | null {
    if (!item.thumbnailCard || !item.thumbnailCardContentType) return null;
    return `data:${item.thumbnailCardContentType};base64,${item.thumbnailCard}`;
  }
}

