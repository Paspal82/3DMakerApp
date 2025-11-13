import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  title = '3dmakerapp.client';
  public welcomeHtml: SafeHtml | null = null;

  constructor(private http: HttpClient, private sanitizer: DomSanitizer) {}

  ngOnInit(): void {
    this.loadWelcome();
  }

  loadWelcome() {
    this.http.get('/welcome', { responseType: 'text' }).subscribe({
      next: (result) => {
        this.welcomeHtml = this.sanitizer.bypassSecurityTrustHtml(result);
      },
      error: (err) => {
        console.error('Failed to load welcome:', err);
      }
    });
  }
}

