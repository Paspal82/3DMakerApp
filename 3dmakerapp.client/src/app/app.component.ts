import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';

interface Product {
  id?: string;
  name: string;
  description: string;
  price: number;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  title = '3dmakerapp.client';
  public products: Product[] = [];

  public newProduct: Product = { name: '', description: '', price: 0 };

  public lastError: string | null = null;
  public lastSuccess: string | null = null;

  // Explicit backend base URL as fallback
  private backendBase = 'https://localhost:55283';

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts() {
    const relativeUrl = `/api/products`;
    console.log('Loading products from (relative) ', relativeUrl);
    this.http.get<Product[]>(relativeUrl).subscribe({
      next: (res) => (this.products = res),
      error: (err) => {
        console.warn('Relative load failed, attempting direct backend URL', err);
        // fallback to direct backend
        const url = `${this.backendBase}/api/products`;
        this.http.get<Product[]>(url).subscribe({
          next: (res2) => (this.products = res2),
          error: (err2) => {
            console.error('Failed to load products from backend', err2);
            this.lastError = 'Failed to load products: ' + (err2?.message || err2);
          }
        });
      }
    });
  }

  createProduct() {
    console.log('createProduct called', this.newProduct);
    this.lastError = null;
    this.lastSuccess = null;
    if (!this.newProduct.name) {
      this.lastError = 'Name is required';
      return;
    }

    const relativeUrl = `/api/products`;
    console.log('Posting to (relative) ', relativeUrl);
    this.http.post<Product>(relativeUrl, this.newProduct).subscribe({
      next: (created) => {
        console.log('Product created via relative proxy', created);
        this.products.push(created);
        this.newProduct = { name: '', description: '', price: 0 };
        this.lastSuccess = 'Product created';
      },
      error: (err) => {
        console.warn('Relative POST failed, attempting direct backend', err);
        // on network error (status 0) try direct backend
        const url = `${this.backendBase}/api/products`;
        console.log('Posting to direct backend', url);
        this.http.post<Product>(url, this.newProduct).subscribe({
          next: (created2) => {
            console.log('Product created via direct backend', created2);
            this.products.push(created2);
            this.newProduct = { name: '', description: '', price: 0 };
            this.lastSuccess = 'Product created (direct)';
          },
          error: (err2) => {
            console.error('Failed to create product on direct backend', err2);
            this.lastError = 'Failed to create product: ' + (err2?.message || err2);
          }
        });
      }
    });
  }
}

