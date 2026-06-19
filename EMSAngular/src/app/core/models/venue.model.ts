export interface VenueDto {
  id: number;
  name: string;
  address: string;
  city: string;
  totalCapacity: number;
  layoutConfig: string;
  createdAt: string;
}

export interface CreateVenueRequest {
  name: string;
  address: string;
  city: string;
  totalCapacity: number;
  layoutConfig: string;
}

export interface UpdateVenueRequest extends CreateVenueRequest {}
