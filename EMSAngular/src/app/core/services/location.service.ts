import { Injectable } from '@angular/core';

interface NominatimReverseResponse {
  address?: Record<string, string>;
}

/**
 * Resolves the user's current city from the browser Geolocation API,
 * reverse-geocoded via OpenStreetMap Nominatim, and matched against the
 * set of cities we actually have events in.
 */
@Injectable({ providedIn: 'root' })
export class LocationService {
  // Address fields Nominatim may use to name a city-level place.
  private readonly cityFields = [
    'city', 'town', 'municipality', 'village',
    'county', 'state_district', 'suburb',
  ];

  /**
   * Returns the matching known city, or null if the user denies permission,
   * geolocation is unavailable, or the detected place is not a known city.
   * Never rejects — callers treat null as "show all cities".
   */
  async detectCity(knownCities: string[]): Promise<string | null> {
    const coords = await this.currentPosition();
    if (!coords) return null;

    const place = await this.reverseGeocode(coords.latitude, coords.longitude);
    if (!place) return null;

    return this.matchKnownCity(place, knownCities);
  }

  private currentPosition(): Promise<GeolocationCoordinates | null> {
    if (!('geolocation' in navigator)) return Promise.resolve(null);

    return new Promise(resolve => {
      navigator.geolocation.getCurrentPosition(
        pos => resolve(pos.coords),
        () => resolve(null),
        { timeout: 10_000, maximumAge: 300_000 },
      );
    });
  }

  private async reverseGeocode(lat: number, lon: number): Promise<Record<string, string> | null> {
    try {
      const url = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${lat}&lon=${lon}`;
      // Native fetch (not HttpClient) so the JWT interceptor never attaches
      // our Authorization header to this third-party request.
      const res = await fetch(url, { headers: { Accept: 'application/json' } });
      if (!res.ok) return null;
      const body = (await res.json()) as NominatimReverseResponse;
      return body.address ?? null;
    } catch {
      return null;
    }
  }

  private matchKnownCity(address: Record<string, string>, knownCities: string[]): string | null {
    const values = this.cityFields
      .map(field => address[field])
      .filter((v): v is string => !!v)
      .map(v => v.toLowerCase());

    for (const city of knownCities) {
      const needle = city.toLowerCase();
      if (values.some(v => v === needle || v.includes(needle))) return city;
    }
    return null;
  }
}
