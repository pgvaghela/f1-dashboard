export interface DriverStanding {
  position: number;
  driverId: number;
  firstName: string;
  lastName: string;
  code: string;
  nationality: string;
  teamName: string;
  totalPoints: number;
}

export interface ConstructorStanding {
  position: number;
  constructorId: number;
  teamName: string;
  nationality: string;
  totalPoints: number;
}
